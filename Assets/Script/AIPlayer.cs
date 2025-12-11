using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AIPersonality
{
    Tight,      // 보수적 (좋은 손만 플레이)
    Normal,     // 중간 (균형잡힌 플레이)
    Aggressive, // 공격적 (자주 레이즈)
    Loose       // 공격적이고 많은 손 플레이
}

public class AIPlayer : MonoBehaviour
{
    [Header("AI 설정")]
    public AIPersonality personality = AIPersonality.Normal;
    public float actionDelay = 1.5f;

    [Header("AI 파라미터")]
    public float foldWeight = 0.5f;
    public float callWeight = 0.5f;
    public float raiseWeight = 0.5f;

    private Player playerComponent;
    private GameManager gm;
    private Deck deck;
    private Coroutine actionCoroutine;

    private void Awake()
    {
        playerComponent = GetComponent<Player>();
        if (playerComponent == null)
        {
            Debug.LogError("[AIPlayer] Player 컴포넌트를 찾을 수 없습니다!");
        }
    }

    private void Start()
    {
        gm = GameObject.Find("GameManager").GetComponent<GameManager>();
        deck = GameObject.Find("Deck").GetComponent<Deck>();

        SetPersonalityWeights();
    }

    private void Update()
    {
        if (playerComponent != null && playerComponent.isMyTurn)
        {
            if (actionCoroutine == null)
            {
                actionCoroutine = StartCoroutine(Co_PerformAction());
            }
        }
    }

    private void SetPersonalityWeights()
    {
        switch (personality)
        {
            case AIPersonality.Tight:
                foldWeight = 0.7f;
                callWeight = 0.2f;
                raiseWeight = 0.1f;
                actionDelay = 1.2f;
                break;

            case AIPersonality.Normal:
                foldWeight = 0.5f;
                callWeight = 0.35f;
                raiseWeight = 0.15f;
                actionDelay = 1.5f;
                break;

            case AIPersonality.Aggressive:
                foldWeight = 0.2f;
                callWeight = 0.4f;
                raiseWeight = 0.4f;
                actionDelay = 0.8f;
                break;

            case AIPersonality.Loose:
                foldWeight = 0.3f;
                callWeight = 0.3f;
                raiseWeight = 0.4f;
                actionDelay = 0.5f;
                break;
        }
    }

    private IEnumerator Co_PerformAction()
    {
        yield return new WaitForSeconds(actionDelay);

        if (playerComponent != null && playerComponent.isMyTurn)
        {
            ExecuteAction();
        }

        actionCoroutine = null;
    }

    private void ExecuteAction()
    {
        if (!playerComponent.canPlay || playerComponent.playerChip <= 0)
        {
            Debug.LogWarning($"[AIPlayer] {gameObject.name}은 더 이상 게임에 참여할 수 없습니다.");
            return;
        }

        HandValue handValue = EvaluateHand();
        int callCost = CalculateCallCost();
        int potOdds = gm.pots > 0 ? callCost * 100 / gm.pots : 0;
        int stackSize = playerComponent.playerChip;

        AIAction action = DecideAction(handValue, callCost, potOdds, stackSize);
        ExecuteAIAction(action);

        Debug.Log($"[AIPlayer] {gameObject.name} - 성격: {personality}, 액션: {action.actionType}, 금액: {action.amount}");
    }

    private HandValue EvaluateHand()
    {
        var holeCards = playerComponent.GetComponentsInChildren<Card>();
        if (holeCards.Length < 2)
            return new HandValue(HandCategory.HighCard, new int[] { 0 });

        var holeList = new List<CardData>();
        foreach (var card in holeCards)
            holeList.Add(card.cardData);

        var boardCards = deck.GetBoardCardData();

        // 자동으로 프리플랍/플랍/턴/리버 모두 처리됨
        return HandEvaluator.EvaluateBestFromHoleAndBoard(holeList, boardCards);
    }

    private HandValue EvaluatePreflopHand(List<CardData> holeCards)
    {
        if (holeCards.Count < 2) return new HandValue(HandCategory.HighCard, new int[] { 0 });

        int rank1 = holeCards[0].cardNumber;
        int rank2 = holeCards[1].cardNumber;
        int suit1 = holeCards[0].cardShape;
        int suit2 = holeCards[1].cardShape;

        bool isPair = rank1 == rank2;
        bool isSuited = suit1 == suit2;

        int score = (rank1 + rank2) * 10;
        if (isPair) score += 1000;
        if (isSuited) score += 100;

        return new HandValue(HandCategory.OnePair, new int[] { score });
    }

    private int CalculateCallCost()
    {
        int callAmount = gm.beforeBettingChip;
        int alreadyContributed = playerComponent.contributedThisRound;
        int additionalAmount = callAmount - alreadyContributed;

        return Mathf.Max(0, additionalAmount);
    }

    private AIAction DecideAction(HandValue handValue, int callCost, int potOdds, int stackSize)
    {
        if (callCost >= stackSize)
        {
            return new AIAction(ActionType.AllIn, stackSize);
        }

        float handStrength = CalculateHandStrength(handValue);
        float randomVariance = Random.Range(-0.2f, 0.2f);
        float adjustedStrength = Mathf.Clamp01(handStrength + randomVariance);
        float rand = Random.value;

        if (callCost == 0)
        {
            if (rand < 0.3f || handStrength < 0.3f)
                return new AIAction(ActionType.Check, 0);
            else if (rand < 0.7f)
                return new AIAction(ActionType.Bet, CalculateBetSize(stackSize, 0.5f));
            else
                return new AIAction(ActionType.Bet, CalculateBetSize(stackSize, 1.0f));
        }

        if (rand < foldWeight)
        {
            if (handStrength < 0.4f)
                return new AIAction(ActionType.Fold, 0);
        }

        if (rand < foldWeight + callWeight)
        {
            return new AIAction(ActionType.Call, callCost);
        }

        int raiseAmount = CalculateRaiseAmount(callCost, stackSize);
        return new AIAction(ActionType.Raise, raiseAmount);
    }

    private float CalculateHandStrength(HandValue handValue)
    {
        float baseScore = (float)handValue.Category / 8f;

        if (handValue.Tiebreakers.Count > 0)
        {
            float tieScore = (handValue.Tiebreakers[0] - 2) / 12f;
            baseScore = (baseScore + Mathf.Clamp01(tieScore)) / 2f;
        }

        return Mathf.Clamp01(baseScore);
    }

    private int CalculateBetSize(int stackSize, float aggression)
    {
        int potBet = (int)(gm.pots * (0.3f + aggression * 0.7f));
        int maxBet = Mathf.Min(potBet, stackSize);

        return Mathf.Max(gm.bigBlind, maxBet);
    }

    private int CalculateRaiseAmount(int callCost, int stackSize)
    {
        int minRaise = callCost * 2;
        int maxRaise = stackSize;
        float aggression = raiseWeight;
        int raiseSize = (int)Mathf.Lerp(callCost, maxRaise, aggression);

        return Mathf.Clamp(raiseSize, minRaise, maxRaise);
    }

    private void ExecuteAIAction(AIAction action)
    {
        switch (action.actionType)
        {
            case ActionType.Fold:
                playerComponent.Fold();
                break;
            case ActionType.Check:
                playerComponent.Check();
                break;
            case ActionType.Call:
                playerComponent.Call();
                break;
            case ActionType.Bet:
                playerComponent.Betting(action.amount);
                break;
            case ActionType.Raise:
                playerComponent.Raise(action.amount);
                break;
            case ActionType.AllIn:
                playerComponent.Allin();
                break;
        }
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