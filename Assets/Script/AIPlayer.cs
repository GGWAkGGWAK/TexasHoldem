using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AIPersonality
{
    Tight,
    Normal,
    Aggressive,
    Loose
}

public class AIPlayer : MonoBehaviour
{
    public AIPersonality personality = AIPersonality.Normal;
    public float actionDelay = 1.5f;

    private const int EquitySamples = 160;
    private Player playerComponent;
    private GameManager gm;
    private Deck deck;
    private Coroutine actionCoroutine;

    private void Awake()
    {
        playerComponent = GetComponent<Player>();
    }

    private void Start()
    {
        gm = GameObject.Find("GameManager").GetComponent<GameManager>();
        deck = GameObject.Find("Deck").GetComponent<Deck>();

        if (Mathf.Approximately(actionDelay, 1.5f))
        {
            switch (personality)
            {
                case AIPersonality.Tight: actionDelay = 1.2f; break;
                case AIPersonality.Aggressive: actionDelay = 0.8f; break;
                case AIPersonality.Loose: actionDelay = 0.5f; break;
            }
        }
    }

    private void Update()
    {
        if (playerComponent != null && playerComponent.isMyTurn && actionCoroutine == null)
            actionCoroutine = StartCoroutine(Co_PerformAction());
    }

    private IEnumerator Co_PerformAction()
    {
        yield return new WaitForSeconds(actionDelay);

        if (playerComponent.isMyTurn && playerComponent.canPlay && playerComponent.playerChip > 0)
            ExecuteAction();

        actionCoroutine = null;
    }

    private void ExecuteAction()
    {
        int callCost = Mathf.Min(gm.GetCallCost(playerComponent), playerComponent.playerChip);
        var cards = playerComponent.GetComponentsInChildren<Card>();
        var board = deck.GetBoardCardData();

        AIAction action = new AIAction(callCost == 0 ? ActionType.Check : ActionType.Fold, 0);
        if (cards.Length == 2)
        {
            var hole = new List<CardData> { cards[0].cardData, cards[1].cardData };
            action = DecideAction(hole, board, callCost);
        }

        switch (action.actionType)
        {
            case ActionType.Fold: playerComponent.Fold(); break;
            case ActionType.Check: playerComponent.Check(); break;
            case ActionType.Call: playerComponent.Call(); break;
            case ActionType.Bet:
            case ActionType.Raise: playerComponent.Raise(action.amount); break;
        }
    }

    private AIAction DecideAction(List<CardData> hole, List<CardData> board, int callCost)
    {
        int effectiveRaiseTo = 0;
        int limpers = 0;
        var opponents = new List<Player>();
        bool preflop = gm.currentStreet == Street.Preflop;
        bool inPosition = gm.IsInPosition(playerComponent);
        var bigBlindPlayer = gm.BigBlindPlayer;

        foreach (var player in gm.turnOrder)
        {
            if (player == null || player == playerComponent || !player.canPlay) continue;

            if (player.playerChip > 0)
                effectiveRaiseTo = Mathf.Max(effectiveRaiseTo, player.contributedThisRound + player.playerChip);

            int posted = player.contributedThisRound;
            if (preflop && player != bigBlindPlayer && posted == gm.bigBlind)
                limpers++;

            opponents.Add(player);
        }

        if (opponents.Count == 0)
            return new AIAction(ActionType.Check, 0);

        int ownRaiseTo = playerComponent.contributedThisRound + playerComponent.playerChip;
        effectiveRaiseTo = Mathf.Min(effectiveRaiseTo, ownRaiseTo);
        bool canRaise = gm.CanIncreaseBet(playerComponent) && effectiveRaiseTo > gm.beforeBettingChip;
        var raisedPreflop = opponents.ConvertAll(p => preflop && p.contributedThisRound > gm.bigBlind);
        var callPots = new List<(int amount, int opponentMask)>();
        int guaranteedReturn = 0;
        int contestedChips = 0;
        if (callCost > 0)
        {
            foreach (var pot in SidePot.BuildPots(gm.turnOrder, playerComponent, callCost))
            {
                if (pot.uncalledTo != null)
                {
                    if (pot.uncalledTo == playerComponent) guaranteedReturn += pot.amount;
                    continue;
                }

                if (!pot.eligible.Contains(playerComponent)) continue;

                // Each bit identifies an opponent eligible to win this pot.
                int mask = 0;
                for (int i = 0; i < opponents.Count; i++)
                    if (pot.eligible.Contains(opponents[i])) mask |= 1 << i;

                if (mask == 0)
                    guaranteedReturn += pot.amount;
                else
                {
                    callPots.Add((pot.amount, mask));
                    contestedChips += pot.amount;
                }
            }
        }

        float equity = EstimateEquity(hole, board, raisedPreflop, callPots, out float expectedContestedReturn);
        float callEquity = expectedContestedReturn / Mathf.Max(1, contestedChips);
        float potOdds = Mathf.Max(0, callCost - guaranteedReturn) / (float)Mathf.Max(1, contestedChips);
        bool multiway = opponents.Count > 1;
        float roll = Random.value;

        if (preflop)
        {
            bool unraised = gm.beforeBettingChip <= gm.bigBlind;
            float openingThreshold = inPosition ? 0.47f : 0.55f;
            if (opponents.Count > 2)
                openingThreshold += 0.02f * (opponents.Count - 2);

            float handScore = PreflopScore(hole[0], hole[1]);
            bool shouldRaise = unraised
                ? handScore >= openingThreshold && roll < 0.90f
                : handScore >= 0.70f && equity >= (multiway ? 0.40f : 0.54f) && roll < 0.85f;

            if (canRaise && shouldRaise)
            {
                // Add 1 BB per limper to the opening size.
                int target = unraised
                    ? Mathf.RoundToInt(gm.bigBlind * ((inPosition ? 2.5f : 3f) + limpers))
                    : gm.beforeBettingChip * (inPosition ? 3 : 4);
                return AggressiveAction(target, effectiveRaiseTo, ownRaiseTo);
            }

            // Discount equity for hands that are hard to realize over three more streets.
            float realization = (inPosition ? 0.90f : 0.80f) * (0.70f + 0.30f * handScore);
            return PassiveAction(callCost, callEquity * (callCost == playerComponent.playerChip ? 1f : realization), potOdds, roll);
        }

        bool river = gm.currentStreet == Street.River;
        bool wetBoard = IsWetBoard(board);
        bool facingBet = callCost > 0;
        float potAfterCall = gm.pots + callCost;
        float effectiveStack = Mathf.Max(0, effectiveRaiseTo - gm.beforeBettingChip);
        float stackToPot = effectiveStack / Mathf.Max(1f, potAfterCall);
        float fraction = facingBet || wetBoard || river ? 0.75f : (multiway ? 0.33f : 0.50f);
        if (stackToPot <= 1f)
            fraction = Mathf.Min(1f, stackToPot);

        // Mix strong hands into the checking range and keep bluffs mostly heads-up.
        float valueThreshold = multiway ? 0.59f : 0.67f;
        if (facingBet) valueThreshold += 0.10f;
        bool valueBet = equity >= valueThreshold && roll < (inPosition ? 0.90f : 0.78f);
        bool semiBluff = !river && equity >= 0.30f && equity <= 0.48f
            && roll < (inPosition ? 0.20f : 0.10f);
        bool riverBluff = river && !facingBet && inPosition && equity <= 0.18f && roll < 0.12f;
        bool bluff = !multiway && stackToPot >= 1f && (semiBluff || riverBluff);
        if (canRaise && (valueBet || bluff))
        {
            int target = gm.beforeBettingChip + Mathf.RoundToInt(potAfterCall * fraction);
            return AggressiveAction(target, effectiveRaiseTo, ownRaiseTo);
        }

        float adjustedEquity = callEquity;
        if (facingBet)
        {
            float rangeDiscount = 0.06f + 0.15f * potOdds;
            adjustedEquity *= 1f - rangeDiscount;
            if (!river && callCost < playerComponent.playerChip && !inPosition)
                adjustedEquity *= 0.88f;
        }

        return PassiveAction(callCost, adjustedEquity, potOdds, roll);
    }

    private AIAction AggressiveAction(int target, int effectiveRaiseTo, int ownRaiseTo)
    {
        // A raise may go under the minimum only if it uses our full stack.
        int legalTarget = Mathf.Min(ownRaiseTo, Mathf.Max(gm.MinimumRaiseTo, Mathf.Min(target, effectiveRaiseTo)));
        return new AIAction(gm.beforeBettingChip == 0 ? ActionType.Bet : ActionType.Raise, legalTarget);
    }

    private static AIAction PassiveAction(int callCost, float equity, float potOdds, float roll)
    {
        if (callCost == 0)
            return new AIAction(ActionType.Check, 0);

        if (potOdds <= 0f)
            return new AIAction(ActionType.Call, callCost);

        const float mixBand = 0.03f;
        float callFrequency = Mathf.Clamp01((equity - potOdds + mixBand) / (2f * mixBand));
        return roll < callFrequency ? new AIAction(ActionType.Call, callCost) : new AIAction(ActionType.Fold, 0);
    }

    private static float EstimateEquity(List<CardData> hole, List<CardData> board, List<bool> raisedPreflop,
        List<(int amount, int opponentMask)> callPots, out float expectedContestedReturn)
    {
        var unseen = new List<CardData>(52);
        for (int suit = 0; suit < 4; suit++)
        {
            for (int rank = 2; rank <= 14; rank++)
            {
                if (!hole.Exists(c => c.cardShape == suit && c.cardNumber == rank)
                    && !board.Exists(c => c.cardShape == suit && c.cardNumber == rank))
                    unseen.Add(new CardData(suit, rank, null));
            }
        }

        float shares = 0f;
        float contestedReturn = 0f;
        var runout = new List<CardData>(5);
        var opponentHole = new List<CardData> { default, default };
        var sample = new CardData[unseen.Count];
        for (int trial = 0; trial < EquitySamples; trial++)
        {
            unseen.CopyTo(sample);
            int drawn = 0;
            foreach (bool raised in raisedPreflop)
            {
                int first = drawn;
                DrawCard(sample, ref drawn);
                DrawCard(sample, ref drawn);

                // Mix in one redraw of weak hands for opponents who raised preflop.
                if (raised && PreflopScore(sample[first], sample[first + 1]) < 0.55f)
                {
                    drawn = first;
                    DrawCard(sample, ref drawn);
                    DrawCard(sample, ref drawn);
                }
            }

            runout.Clear();
            runout.AddRange(board);
            while (runout.Count < 5)
                runout.Add(DrawCard(sample, ref drawn));

            HandValue hero = HandEvaluator.EvaluateBestFromHoleAndBoard(hole, runout);
            int betterMask = 0;
            int tiedMask = 0;
            int tied = 1;
            for (int index = 0; index < raisedPreflop.Count; index++)
            {
                opponentHole[0] = sample[index * 2];
                opponentHole[1] = sample[index * 2 + 1];
                HandValue opponent = HandEvaluator.EvaluateBestFromHoleAndBoard(opponentHole, runout);
                int result = HandEvaluator.CompareHands(hero, opponent);
                if (result < 0)
                {
                    betterMask |= 1 << index;
                    if (callPots.Count == 0) break;
                }

                if (result == 0)
                {
                    tiedMask |= 1 << index;
                    tied++;
                }
            }

            if (betterMask == 0) shares += 1f / tied;

            foreach (var pot in callPots)
            {
                if ((betterMask & pot.opponentMask) != 0) continue;

                int winners = 1;
                for (int ties = tiedMask & pot.opponentMask; ties != 0; ties &= ties - 1)
                    winners++;
                contestedReturn += (float)pot.amount / winners;
            }
        }

        expectedContestedReturn = contestedReturn / EquitySamples;
        return shares / EquitySamples;
    }

    private static CardData DrawCard(CardData[] cards, ref int drawn)
    {
        int index = Random.Range(drawn, cards.Length);
        CardData selected = cards[index];
        cards[index] = cards[drawn];
        cards[drawn++] = selected;
        return selected;
    }

    private static float PreflopScore(CardData first, CardData second)
    {
        int high = Mathf.Max(first.cardNumber, second.cardNumber);
        int low = Mathf.Min(first.cardNumber, second.cardNumber);
        if (high == low)
            return 0.54f + (high - 2) * 0.038f;

        float score = (high - 2) * 0.036f + (low - 2) * 0.023f;
        if (first.cardShape == second.cardShape) score += 0.12f;
        if (high - low <= 2) score += 0.06f;
        if (high == 14) score += 0.08f;
        return score;
    }

    private static bool IsWetBoard(List<CardData> board)
    {
        var suits = new int[4];
        foreach (var card in board)
        {
            if (++suits[card.cardShape] >= (board.Count == 3 ? 2 : 3))
                return true;

            int nearbyRanks = 0;
            foreach (var other in board)
                if (other.cardNumber > card.cardNumber && other.cardNumber <= card.cardNumber + 4)
                    nearbyRanks |= 1 << other.cardNumber;

            if ((nearbyRanks & (nearbyRanks - 1)) != 0)
                return true;
        }

        return false;
    }
}

public struct AIAction
{
    public ActionType actionType;
    public int amount;

    public AIAction(ActionType type, int amt)
    {
        actionType = type;
        amount = amt;
    }
}
