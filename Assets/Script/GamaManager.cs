using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum Street { Preflop, Flop, Turn, River, Showdown }
public enum ActionType { Fold, Check, Call, Bet, Raise, AllIn }

public class GamaManager : MonoBehaviour
{
    [Header("Blinds / Pots")]
    public int smallBlind;
    public int BigBlind;
    public int pots;
    public int beforeBettingChip;
    public int beforeRaiseChip;

    [Header("Round / UI")]
    public float duration;
    public float bettingTime;
    public Text potsText;

    [Header("Turn / Street")]
    public List<Player> turnOrder = new List<Player>();
    public int currentIndex = -1;
    public Street currentStreet = Street.Preflop;

    public List<Seat> seatOrder = new List<Seat>();
    public int buttonIndex = 9;  // 첫 게임: 10번 좌석
    public int sbIndex = -1;
    public int bbIndex = -1;

    private int lastAggressorIndex = -1;
    private int actorsToAct = 0;
    public float nextHandDelay = 3f;

    private void Awake()
    {
        Transform canvas = GameObject.Find("Canvas").transform;
        potsText = canvas.Find("팟").GetComponent<Text>();
    }

    private IEnumerator Start()
    {
        smallBlind = 10000;
        BigBlind = 20000;
        duration = 180;

        yield return null;

        BuildSeatOrder();
        BuildTurnOrderBySeats();

        RotateButtonToNextOccupied(fixedButtonStart: true);
        BeginNewHand();
    }

    private void Update()
    {
        if (potsText != null)
            potsText.text = "Pots: " + pots.ToString("N0");
    }

    // --------------------- 좌석/턴 ---------------------
    public void BuildSeatOrder()
    {
        seatOrder.Clear();
        var seats = new List<Seat>(FindObjectsOfType<Seat>(true));
        seats.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        seatOrder.AddRange(seats);
    }

    public void BuildTurnOrderBySeats()
    {
        turnOrder.Clear();
        foreach (var seat in seatOrder)
        {
            var p = seat.GetComponentInChildren<Player>(true);
            seat.isSeated = (p != null);
            if (p != null)
                turnOrder.Add(p);
        }
        foreach (var p in turnOrder) p.isMyTurn = false;
    }

    private int NextOccupiedSeatIndex(int from)
    {
        if (seatOrder.Count == 0) return -1;
        for (int step = 1; step <= seatOrder.Count; step++)
        {
            int idx = (from + step) % seatOrder.Count;
            var seat = seatOrder[idx];
            if (seat != null && seat.isSeated) return idx;
        }
        return -1;
    }

    private int TurnIndexFromSeatIndex(int seatIdx)
    {
        if (seatIdx < 0 || seatIdx >= seatOrder.Count) return 0;
        var p = seatOrder[seatIdx].GetComponentInChildren<Player>(true);
        if (p == null) return 0;
        int idx = turnOrder.IndexOf(p);
        return (idx >= 0) ? idx : 0;
    }

    // --------------------- 버튼 회전 ---------------------
    public void RotateButtonToNextOccupied(bool fixedButtonStart = false)
    {
        if (seatOrder.Count == 0) BuildSeatOrder();

        if (fixedButtonStart)
        {
            buttonIndex = 9; // 첫 핸드 10번
            if (seatOrder[buttonIndex] == null || !seatOrder[buttonIndex].isSeated)
                buttonIndex = NextOccupiedSeatIndex(9);
        }
        else
        {
            buttonIndex = NextOccupiedSeatIndex(buttonIndex);
        }

        sbIndex = NextOccupiedSeatIndex(buttonIndex);
        bbIndex = NextOccupiedSeatIndex(sbIndex);

        Debug.Log($"[Button] BTN={buttonIndex + 1}, SB={sbIndex + 1}, BB={bbIndex + 1}");
    }

    // --------------------- 핸드 시작 ---------------------
    public void BeginNewHand()
    {
        var deck = FindObjectOfType<Deck>();
        if (deck == null) return;

        pots = 0;
        beforeBettingChip = 0;
        beforeRaiseChip = 0;
        currentStreet = Street.Preflop;

        foreach (var p in turnOrder)
        {
            if (p == null) continue;
            p.isMyTurn = false;
            p.canPlay = (p.playerChip > 0);
        }

        deck.ShuffleDeck();
        PostBlinds();

        var order = BuildPreflopOrderSBtoBTN();
        deck.PreflopDealInOrder(order);

        // ✅ 프리플랍은 UTG(=BB 다음)부터
        int utgSeatIdx = NextOccupiedSeatIndex(bbIndex);
        int utgTurnIdx = TurnIndexFromSeatIndex(utgSeatIdx);
        StartBettingRound(utgTurnIdx);

        Debug.Log($"[NewHand] BTN={buttonIndex + 1}, SB={sbIndex + 1}, BB={bbIndex + 1}, UTG={utgSeatIdx + 1}");
    }

    private List<Seat> BuildPreflopOrderSBtoBTN()
    {
        var order = new List<Seat>();
        if (sbIndex < 0 || buttonIndex < 0) return order;
        int cur = sbIndex;
        while (true)
        {
            var seat = seatOrder[cur];
            if (seat != null && seat.isSeated) order.Add(seat);
            if (cur == buttonIndex) break;
            cur = (cur + 1) % seatOrder.Count;
        }
        return order;
    }

    private void PostBlinds()
    {
        var sbSeat = seatOrder[sbIndex];
        var bbSeat = seatOrder[bbIndex];
        var sb = sbSeat.GetComponentInChildren<Player>(true);
        var bb = bbSeat.GetComponentInChildren<Player>(true);

        int sbPay = Mathf.Min(smallBlind, sb.playerChip);
        int bbPay = Mathf.Min(BigBlind, bb.playerChip);

        sb.playerChip -= sbPay;
        bb.playerChip -= bbPay;
        pots += sbPay + bbPay;

        beforeBettingChip = bbPay;
        beforeRaiseChip = 0;

        Debug.Log($"[Blinds] SB={sb.name}:{sbPay}, BB={bb.name}:{bbPay}");
    }

    // --------------------- 베팅 라운드 ---------------------
    public void StartBettingRound(int firstTurnIndex)
    {
        if (currentStreet != Street.Preflop)
        {
            beforeBettingChip = 0;
            beforeRaiseChip = 0;
        }

        foreach (var p in turnOrder)
            if (p != null) p.isMyTurn = false;

        currentIndex = Mathf.Clamp(firstTurnIndex, 0, turnOrder.Count - 1);
        turnOrder[currentIndex].isMyTurn = true;

        lastAggressorIndex = -1;
        actorsToAct = ActivePlayersCount();
        Debug.Log($"[RoundStart] {currentStreet}, First={turnOrder[currentIndex].name}");
    }

    public int ActivePlayersCount()
    {
        int cnt = 0;
        foreach (var p in turnOrder)
            if (p != null && p.canPlay && p.playerChip > 0)
                cnt++;
        return cnt;
    }

    public void RegisterAction(Player actor, ActionType action, bool isRaise)
    {
        if (action == ActionType.Fold)
        {
            actorsToAct = Mathf.Max(actorsToAct - 1, 0);
        }
        else if (isRaise)
        {
            lastAggressorIndex = turnOrder.IndexOf(actor);
            actorsToAct = ActivePlayersCount() - 1;
        }
        else
        {
            actorsToAct = Mathf.Max(actorsToAct - 1, 0);
        }

        int alive = ActivePlayersCount();
        if (alive <= 1)
        {
            WinByAllFold();
            return;
        }

        if (actorsToAct == 0)
            AdvanceStreet();
    }

    public void NextTurnFrom(Player actor)
    {
        if (turnOrder.Count == 0) return;
        int idx = turnOrder.IndexOf(actor);
        if (idx < 0) idx = currentIndex;
        actor.isMyTurn = false;

        for (int step = 1; step <= turnOrder.Count; step++)
        {
            int next = (idx + step) % turnOrder.Count;
            var cand = turnOrder[next];
            if (cand != null && cand.canPlay && cand.playerChip > 0)
            {
                currentIndex = next;
                cand.isMyTurn = true;
                return;
            }
        }
    }

    public void HandleFoldAndPassTurn(Player actor)
    {
        if (turnOrder.Count == 0) return;
        int removedIndex = turnOrder.IndexOf(actor);
        if (removedIndex < 0) removedIndex = currentIndex;

        actor.isMyTurn = false;
        turnOrder.RemoveAt(removedIndex);

        if (ActivePlayersCount() <= 1)
        {
            WinByAllFold();
            return;
        }

        currentIndex = removedIndex % turnOrder.Count;

        for (int step = 0; step < turnOrder.Count; step++)
        {
            int next = (currentIndex + step) % turnOrder.Count;
            var cand = turnOrder[next];
            if (cand != null && cand.canPlay && cand.playerChip > 0)
            {
                foreach (var p in turnOrder) if (p != null) p.isMyTurn = false;
                currentIndex = next;
                cand.isMyTurn = true;
                return;
            }
        }
    }

    // --------------------- 스트리트 전환 ---------------------
    public void AdvanceStreet()
    {
        var deck = FindObjectOfType<Deck>();
        switch (currentStreet)
        {
            case Street.Preflop:
                currentStreet = Street.Flop;
                deck.Plop();
                // ✅ 플랍 이후 SB부터 액션 시작
                StartBettingRound(TurnIndexFromSeatIndex(sbIndex));
                break;

            case Street.Flop:
                currentStreet = Street.Turn;
                deck.Turn();
                StartBettingRound(TurnIndexFromSeatIndex(sbIndex));
                break;

            case Street.Turn:
                currentStreet = Street.River;
                deck.River();
                StartBettingRound(TurnIndexFromSeatIndex(sbIndex));
                break;

            case Street.River:
                currentStreet = Street.Showdown;
                ResolveShowdown();
                break;
        }
    }

    // --------------------- 쇼다운/올폴드 ---------------------
    private void ResolveShowdown()
    {
        var deck = FindObjectOfType<Deck>();
        List<CardData> board5 = deck.GetBoardCardData();

        var activePlayers = new List<Player>();
        foreach (var p in turnOrder)
            if (p != null && p.canPlay)
                activePlayers.Add(p);

        var winners = WinnerEvaluator.DecideWinners(activePlayers, board5);
        WinnerEvaluator.DistributePot(pots, winners);
        pots = 0;

        StartCoroutine(Co_NextHandAfterDelay(nextHandDelay));
    }

    private void WinByAllFold()
    {
        var alive = new List<Player>();
        foreach (var p in turnOrder)
            if (p != null && p.canPlay)
                alive.Add(p);

        if (alive.Count == 1)
        {
            alive[0].playerChip += pots;
            pots = 0;
            Debug.Log($"[AllFold] {alive[0].name} wins by default.");
        }

        StartCoroutine(Co_NextHandAfterDelay(nextHandDelay));
    }

    private IEnumerator Co_NextHandAfterDelay(float sec)
    {
        yield return new WaitForSeconds(sec);
        RotateButtonToNextOccupied(fixedButtonStart: false);
        BeginNewHand();
    }
}
