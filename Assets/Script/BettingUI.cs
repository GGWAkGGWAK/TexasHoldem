using UnityEngine;
using UnityEngine.UI;

public class BettingUI : MonoBehaviour
{
    private GameManager gm;
    private Button[] bettingButtons;
    private Button startButton;
    private Button resetButton;

    private void Awake()
    {
        gm = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>();
        var canvas = GameObject.Find("Canvas").transform;

        var foldButton = canvas.Find("폴드").GetComponent<Button>();
        var checkButton = canvas.Find("체크").GetComponent<Button>();
        var callButton = canvas.Find("콜").GetComponent<Button>();
        var raiseButton = canvas.Find("레이즈").GetComponent<Button>();
        var allInButton = canvas.Find("올인").GetComponent<Button>();
        bettingButtons = new[] { foldButton, checkButton, callButton, raiseButton, allInButton };
        startButton = canvas.Find("스타트")?.GetComponent<Button>();
        resetButton = canvas.Find("초기화")?.GetComponent<Button>();

        foreach (var button in bettingButtons)
            button.onClick.RemoveAllListeners();

        foldButton.onClick.AddListener(OnClickFold);
        checkButton.onClick.AddListener(OnClickCheck);
        callButton.onClick.AddListener(OnClickCall);
        raiseButton.onClick.AddListener(OnClickRaise);
        allInButton.onClick.AddListener(OnClickAllin);
    }

    private void Update()
    {
        bool canAct = CurrentHumanPlayer() != null;
        foreach (var button in bettingButtons)
            button.interactable = canAct;

        if (startButton != null) startButton.interactable = gm.CanStartHand;
        if (resetButton != null) resetButton.interactable = gm.CanStartHand;
    }

    private Player CurrentHumanPlayer()
    {
        if (gm.currentIndex < 0 || gm.currentIndex >= gm.turnOrder.Count) return null;

        var player = gm.turnOrder[gm.currentIndex];
        if (player == null || !player.isMyTurn) return null;

        var ai = player.GetComponent<AIPlayer>();
        return ai != null && ai.isActiveAndEnabled ? null : player;
    }

    public void OnClickFold() => CurrentHumanPlayer()?.Fold();
    public void OnClickCheck() => CurrentHumanPlayer()?.Check();
    public void OnClickCall() => CurrentHumanPlayer()?.Call();
    public void OnClickRaise() => CurrentHumanPlayer()?.OnRaiseButtonClicked();
    public void OnClickAllin() => CurrentHumanPlayer()?.Allin();
}
