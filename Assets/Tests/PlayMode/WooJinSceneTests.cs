using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class WooJinSceneTests
{
    private Component game;
    private Button start;
    private Button reset;
    private Button addPlayer;
    private Button fold;
    private Button check;
    private Button call;
    private Text winner;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        yield return SceneManager.LoadSceneAsync("WooJinScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        game = GameObject.FindWithTag("GameManager").GetComponent("GameManager");
        Assert.That(game, Is.Not.Null);
        start = CanvasControl<Button>("스타트");
        reset = CanvasControl<Button>("초기화");
        addPlayer = CanvasControl<Button>("추가");
        fold = CanvasControl<Button>("폴드");
        check = CanvasControl<Button>("체크");
        call = CanvasControl<Button>("콜");
        winner = CanvasControl<Text>("승자표시");
    }

    [UnityTest]
    public IEnumerator LiveHandRejectsManualRestartAndAiTurnInput()
    {
        addPlayer.onClick.Invoke();
        // Seat 2 is the big blind. Keep the two-player opening order deterministic.
        var addAi = game.GetType().GetMethod("AddNewAIPlayerToEmptySeat");
        addAi.Invoke(game, new[] { addAi.GetParameters()[0].DefaultValue });
        var ai = GameObject.Find("Table").GetComponentsInChildren<MonoBehaviour>()
            .Single(behaviour => behaviour.GetType().Name == "AIPlayer");
        ai.GetType().GetField("actionDelay").SetValue(ai, 2f);

        Assert.That(start.interactable, Is.True);
        Assert.That(reset.interactable, Is.True);
        start.onClick.Invoke();
        yield return null;

        var players = Players();
        Assert.That(players, Has.Length.EqualTo(2));
        var originalCards = players.SelectMany(HoleCards).ToArray();
        Assert.That(originalCards, Has.Length.EqualTo(4));
        var originalStacks = players.Select(Stack).ToArray();
        int blindPot = Pot();
        Assert.That(blindPot, Is.EqualTo(Field<int>(game, "smallBlind") + Field<int>(game, "bigBlind")));
        Assert.That(start.interactable, Is.False);
        Assert.That(reset.interactable, Is.False);
        Assert.That(call.interactable, Is.True);

        // Invoke the serialized callbacks directly as well as checking their disabled UI.
        start.onClick.Invoke();
        reset.onClick.Invoke();
        yield return null;
        Assert.That(Pot(), Is.EqualTo(blindPot));
        Assert.That(players.Select(Stack), Is.EqualTo(originalStacks));
        Assert.That(players.SelectMany(HoleCards), Is.EqualTo(originalCards));

        call.onClick.Invoke();
        yield return null;
        var aiPlayer = ai.GetComponent("Player");
        Assert.That(Field<bool>(aiPlayer, "isMyTurn"), Is.True);
        Assert.That(fold.interactable, Is.False);
        Assert.That(check.interactable, Is.False);
        Assert.That(call.interactable, Is.False);
        int calledPot = Pot();
        fold.onClick.Invoke();
        Assert.That(Field<bool>(aiPlayer, "canPlay"), Is.True);
        Assert.That(Pot(), Is.EqualTo(calledPot));

        yield return WaitFor(() => fold.interactable, "AI should act and return control to the human player.");
        fold.onClick.Invoke();
        yield return null;
        Assert.That(winner.gameObject.activeSelf, Is.True);
        Assert.That(winner.text, Does.Contain("All Fold"));
        Assert.That(Pot(), Is.Zero);
        Assert.That(start.interactable, Is.False);
        Assert.That(reset.interactable, Is.False);

        start.onClick.Invoke();
        reset.onClick.Invoke();
        Assert.That(Pot(), Is.Zero);

        yield return WaitFor(() => Pot() == blindPot && !winner.gameObject.activeSelf,
            "Exactly one automatic hand should post a new pair of blinds.");
        yield return null;
        Assert.That(Pot(), Is.EqualTo(blindPot));
        Assert.That(Players().SelectMany(HoleCards).Count(), Is.EqualTo(4));
        Assert.That(originalCards.All(card => card == null), Is.True);
        Assert.That(Players().Sum(Stack) + Pot(), Is.EqualTo(originalStacks.Sum() + blindPot));
    }

    [UnityTest]
    public IEnumerator CheckingThroughShowdownDealsFiveBoardCardsAndConservesChips()
    {
        addPlayer.onClick.Invoke();
        addPlayer.onClick.Invoke();
        start.onClick.Invoke();
        yield return null;

        var players = Players();
        Assert.That(players, Has.Length.EqualTo(2));
        int totalChips = players.Sum(Stack) + Pot();
        Assert.That(players.SelectMany(HoleCards).Count(), Is.EqualTo(4));
        Assert.That(BoardCount(), Is.Zero);

        call.onClick.Invoke();
        check.onClick.Invoke();
        Assert.That(BoardCount(), Is.EqualTo(3));

        check.onClick.Invoke();
        check.onClick.Invoke();
        Assert.That(BoardCount(), Is.EqualTo(4));

        check.onClick.Invoke();
        check.onClick.Invoke();
        Assert.That(BoardCount(), Is.EqualTo(5));

        check.onClick.Invoke();
        check.onClick.Invoke();
        Assert.That(winner.gameObject.activeSelf, Is.True);
        Assert.That(winner.text, Does.Contain("Main pot"));
        Assert.That(Pot(), Is.Zero);
        Assert.That(players.Sum(Stack), Is.EqualTo(totalChips));
    }

    // Runtime scripts are in Unity's predefined Assembly-CSharp. Inspect scene
    // components by name so these tests do not change the game's assembly layout.
    private static T Field<T>(Component component, string name)
    {
        return (T)component.GetType().GetField(name).GetValue(component);
    }

    private Component[] Players() => Field<IEnumerable>(game, "turnOrder").Cast<Component>().ToArray();
    private int Pot() => Field<int>(game, "pots");
    private static int Stack(Component player) => Field<int>(player, "playerChip");

    private static T CanvasControl<T>(string name) where T : Component
    {
        return GameObject.Find("Canvas").transform.Find(name).GetComponent<T>();
    }

    private static MonoBehaviour[] HoleCards(Component player)
    {
        return player.GetComponentsInChildren<MonoBehaviour>()
            .Where(component => component.GetType().Name == "Card").ToArray();
    }

    private static int BoardCount()
    {
        var table = GameObject.Find("Table").transform;
        string[] boardPositions = { "Plop1", "Plop2", "Plop3", "Turn", "River" };
        return boardPositions.Count(position => table.Find(position).childCount > 0);
    }

    private static IEnumerator WaitFor(Func<bool> condition, string message)
    {
        float timeout = Time.realtimeSinceStartup + 12f;
        while (!condition() && Time.realtimeSinceStartup < timeout)
            yield return null;

        Assert.That(condition(), Is.True, message);
    }
}

