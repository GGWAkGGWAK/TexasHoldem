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
    public Text winnerText;

    [Header("Turn / Street")]
    public List<Player> turnOrder = new List<Player>();
    public int currentIndex = -1;
    public Street currentStreet = Street.Preflop;

    [Header("Seats / Button")]
    public List<Seat> seatOrder = new List<Seat>();
    public int buttonIndex = 9;   // 10번(0-base 9)에서 시작, 항상 한 칸식 ++
    public int sbIndex = -1;
    public int bbIndex = -1;

    [Header("Flow")]
    private int lastAggressorIndex = -1;
    private int actorsToAct = 0;
    public float nextHandDelay = 3f;

    private DealerButton dealerButton;

    private IEnumerator Start()
    {
        // UI hookup
        var canvas = GameObject.Find("Canvas").transform;
        potsText = canvas.Find("팟").GetComponent<Text>();
        var wObj = canvas.Find("승자표시");
        if (wObj != null) winnerText = wObj.GetComponent<Text>();
        HideWinnersUI();

        var dbObj = GameObject.Find("DealerButton");
        if (dbObj != null) dealerButton = dbObj.GetComponent<DealerButton>();

        // defaults
        smallBlind = 10000;
        BigBlind = 20000;
        duration = 180;

        yield return null;

        BuildSeatOrder();
        BuildTurnOrderBySeats();

        // 첫 핸드: 버튼을 10번 좌석에 "고정"(비어있어도 이동하지 않음)
        buttonIndex = Mathf.Clamp(9, 0, seatOrder.Count - 1);
        // SB/BB는 '플레이어가 있는 다음 좌석'으로 산정
        sbIndex = NextSeatWithPlayerFrom(buttonIndex);
        bbIndex = NextSeatWithPlayerFrom(sbIndex);
        TeleportDealerButton();

        BeginNewHand();
    }

    private void Update()
    {
        if (potsText != null) potsText.text = "Pots: " + pots.ToString("N0");
    }

    // ========== 좌석/턴 ==========
    public void BuildSeatOrder()
    {
        seatOrder.Clear();
        var seats = new List<Seat>(FindObjectsOfType<Seat>(true));
        seats.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        seatOrder.AddRange(seats);
    }

    private static Player GetPlayerAtSeat(Seat s) => s ? s.GetComponentInChildren<Player>(true) : null;
    private bool SeatHasPlayer(Seat s) => GetPlayerAtSeat(s) != null;

    public void BuildTurnOrderBySeats()
    {
        turnOrder.Clear();
        foreach (var seat in seatOrder)
        {
            var p = GetPlayerAtSeat(seat);
            if (p != null) turnOrder.Add(p);
        }
        foreach (var p in turnOrder) if (p != null) p.isMyTurn = false;
        Debug.Log($"[TurnOrder] players={turnOrder.Count}");
    }

    private int TurnIndexFromSeatIndex(int seatIdx)
    {
        if (seatIdx < 0 || seatIdx >= seatOrder.Count) return 0;
        var p = GetPlayerAtSeat(seatOrder[seatIdx]);
        if (p == null) return 0;
        int idx = turnOrder.IndexOf(p);
        return (idx >= 0) ? idx : 0;
    }

    // ========== 버튼 이동 ==========
    // 다음 핸드로 넘어갈 때 버튼은 "무조건 한 칸" 이동(비었어도 건너뛰지 않음)
    private void AdvanceButtonOneSeat()
    {
        if (seatOrder.Count == 0) return;
        buttonIndex = (buttonIndex + 1) % seatOrder.Count;

        // SB/BB는 '플레이어가 있는' 다음 좌석으로 재산정
        sbIndex = NextSeatWithPlayerFrom(buttonIndex);
        bbIndex = NextSeatWithPlayerFrom(sbIndex);

        Debug.Log($"[Button] BTN={buttonIndex + 1} (moved one seat), SB={sbIndex + 1}, BB={bbIndex + 1}");
    }

    // from 다음 자리부터, Player가 있는 좌석을 찾는다
    private int NextSeatWithPlayerFrom(int from)
    {
        if (seatOrder.Count == 0) return -1;
        int tries = 0;
        int idx = (from + 1) % seatOrder.Count;
        while (tries < seatOrder.Count)
        {
            if (SeatHasPlayer(seatOrder[idx])) return idx;
            idx = (idx + 1) % seatOrder.Count;
            tries++;
        }
        return -1; // 모두 비었을 때
    }

    private void TeleportDealerButton()
    {
        if (dealerButton == null) return;
        var seat = (buttonIndex >= 0 && buttonIndex < seatOrder.Count) ? seatOrder[buttonIndex] : null;
        if (seat != null) dealerButton.TeleportTo(seat.transform);
    }

    private void MoveDealerButton()
    {
        if (dealerButton == null) return;
        var seat = (buttonIndex >= 0 && buttonIndex < seatOrder.Count) ? seatOrder[buttonIndex] : null;
        if (seat != null) dealerButton.MoveTo(seat.transform);
    }

    // ========== 새 핸드 ==========
    public void BeginNewHand()
    {
        HideWinnersUI();

        var deck = FindObjectOfType<Deck>();
        if (deck == null) return;

        pots = 0;
        beforeBettingChip = 0;
        beforeRaiseChip = 0;
        currentStreet = Street.Preflop;

        // 매 핸드 시작마다 좌석 기준으로 턴오더 재구성(폴드/제거 흔적 제거)
        BuildTurnOrderBySeats();
        foreach (var p in turnOrder)
        {
            if (p == null) continue;
            p.isMyTurn = false;
            p.canPlay = (p.playerChip > 0);
            p.isAllIn = false;
            p.contributedThisHand = 0;
        }

        deck.ShuffleDeck();
        PostBlinds();

        // SB → … → BTN 순서로 배분
        var order = BuildPreflopDealingOrder();
        deck.PreflopDealInOrder(order);

        // 프리플랍: BB 다음(=UTG)부터 canPlay==true 첫 플레이어
        int utgSeatIdx = FirstToActPreflopSeatIndex();
        int utgTurnIdx = TurnIndexFromSeatIndex(utgSeatIdx);
        StartBettingRound(utgTurnIdx);

        Debug.Log($"[NewHand] BTN={buttonIndex + 1}, SB={sbIndex + 1}, BB={bbIndex + 1}, UTG={utgSeatIdx + 1}");
    }

    private List<Seat> BuildPreflopDealingOrder()
    {
        var order = new List<Seat>();
        if (sbIndex < 0 || buttonIndex < 0) return order;

        // SB에서 시작해 "플레이어가 있는" 좌석만 모아 BTN까지
        int cur = sbIndex;
        while (true)
        {
            var seat = seatOrder[cur];
            if (SeatHasPlayer(seat)) order.Add(seat);
            if (cur == buttonIndex) break;
            cur = NextSeatWithPlayerFrom(cur);
            if (cur < 0) break;
        }
        return order;
    }

    private void PostBlinds()
    {
        var sbSeat = (sbIndex >= 0 && sbIndex < seatOrder.Count) ? seatOrder[sbIndex] : null;
        var bbSeat = (bbIndex >= 0 && bbIndex < seatOrder.Count) ? seatOrder[bbIndex] : null;
        var sb = GetPlayerAtSeat(sbSeat);
        var bb = GetPlayerAtSeat(bbSeat);

        int sbPay = Mathf.Min(smallBlind, sb.playerChip);
        int bbPay = Mathf.Min(BigBlind, bb.playerChip);

        sb.playerChip -= sbPay;
        bb.playerChip -= bbPay;
        pots += sbPay + bbPay;

        beforeBettingChip = bbPay;
        beforeRaiseChip = 0;

        sb.contributedThisHand += sbPay;
        bb.contributedThisHand += bbPay;

        Debug.Log($"[Blinds] SB={sb.name}:{sbPay}, BB={bb.name}:{bbPay}");
    }

    // ========== 액션 시작자 ==========
    // 프리플랍: BB 다음(=UTG)부터 canPlay==true 첫 플레이어
    private int FirstToActPreflopSeatIndex()
    {
        int idx = bbIndex;
        for (int i = 0; i < seatOrder.Count; i++)
        {
            idx = NextSeatWithPlayerFrom(idx);
            var p = (idx >= 0) ? GetPlayerAtSeat(seatOrder[idx]) : null;
            if (p != null && p.canPlay) return idx;
        }
        return bbIndex; // fallback
    }

    // 포스트플랍: 버튼 다음부터 canPlay==true 첫 플레이어
    private int FirstToActPostflopSeatIndex()
    {
        int idx = buttonIndex;
        for (int i = 0; i < seatOrder.Count; i++)
        {
            idx = NextSeatWithPlayerFrom(idx);
            var p = (idx >= 0) ? GetPlayerAtSeat(seatOrder[idx]) : null;
            if (p != null && p.canPlay) return idx;
        }
        return buttonIndex; // fallback
    }

    // ========== 베팅 라운드 ==========
    public void StartBettingRound(int firstTurnIndex)
    {
        if (currentStreet != Street.Preflop)
        {
            beforeBettingChip = 0;
            beforeRaiseChip = 0;
        }

        foreach (var p in turnOrder) if (p != null) p.isMyTurn = false;

        currentIndex = Mathf.Clamp(firstTurnIndex, 0, turnOrder.Count - 1);
        if (turnOrder.Count > 0) turnOrder[currentIndex].isMyTurn = true;

        lastAggressorIndex = -1;
        actorsToAct = ActivePlayersCount();

        Debug.Log($"[RoundStart] {currentStreet}, First={(turnOrder.Count > 0 ? turnOrder[currentIndex].name : "-")}, Actors={actorsToAct}");
    }

    public int ActivePlayersCount()
    {
        int cnt = 0;
        foreach (var p in turnOrder)
            if (p != null && p.canPlay && p.playerChip > 0) cnt++;
        return cnt;
    }

    public void RegisterAction(Player actor, ActionType action, bool isRaise)
    {
        if (action == ActionType.Fold)
            actorsToAct = Mathf.Max(actorsToAct - 1, 0);
        else if (isRaise)
        {
            lastAggressorIndex = turnOrder.IndexOf(actor);
            actorsToAct = ActivePlayersCount() - 1;
        }
        else
            actorsToAct = Mathf.Max(actorsToAct - 1, 0);

        if (ActivePlayersCount() <= 1)
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

    // ========== 스트리트 전환 ==========
    public void AdvanceStreet()
    {
        var deck = FindObjectOfType<Deck>();
        switch (currentStreet)
        {
            case Street.Preflop:
                currentStreet = Street.Flop;
                deck.Plop();
                // 포스트플랍: 버튼 다음부터 시작 (폴드면 다음 canPlay)
                StartBettingRound(TurnIndexFromSeatIndex(FirstToActPostflopSeatIndex()));
                break;

            case Street.Flop:
                currentStreet = Street.Turn;
                deck.Turn();
                StartBettingRound(TurnIndexFromSeatIndex(FirstToActPostflopSeatIndex()));
                break;

            case Street.Turn:
                currentStreet = Street.River;
                deck.River();
                StartBettingRound(TurnIndexFromSeatIndex(FirstToActPostflopSeatIndex()));
                break;

            case Street.River:
                currentStreet = Street.Showdown;
                ResolveShowdown();
                break;
        }
    }

    // ========== 쇼다운/올폴드 ==========
    private void ResolveShowdown()
    {
        var deck = FindObjectOfType<Deck>();
        List<CardData> board5 = deck.GetBoardCardData();

        var activePlayers = new List<Player>();
        foreach (var p in turnOrder)
            if (p != null && p.canPlay) activePlayers.Add(p);

        // 사이드팟 분배(문자 요약)
        var allPots = SidePot.BuildPots(turnOrder);
        string potsSummary = SidePot.DistributeAllPots(allPots, board5);

        this.pots = 0;

        // 승자 표시 (show purpose)
        var winners = WinnerEvaluator.DecideWinners(activePlayers, board5);
        ShowWinnersUI(winners, board5, suffix: "\n" + potsSummary);

        StartCoroutine(Co_NextHandAfterDelay(nextHandDelay));
    }

    private void WinByAllFold()
    {
        var alive = new List<Player>();
        foreach (var p in turnOrder)
            if (p != null && p.canPlay) alive.Add(p);

        if (alive.Count == 1)
        {
            alive[0].playerChip += pots;
            this.pots = 0;
            ShowWinnersUI(new List<Player> { alive[0] }, null, suffix: " (All Fold)");
        }
        else
        {
            ShowWinnersUI(new List<Player>(), null, suffix: "");
        }

        StartCoroutine(Co_NextHandAfterDelay(nextHandDelay));
    }

    private IEnumerator Co_NextHandAfterDelay(float sec)
    {
        yield return new WaitForSeconds(sec);

        // 다음 핸드: 좌석 재스캔 → 버튼 “한 칸” 이동 → SB/BB 재산정 → 이동 애니메
        BuildSeatOrder();
        AdvanceButtonOneSeat();
        MoveDealerButton();

        HideWinnersUI();

        yield return new WaitForSeconds(0.5f);
        BeginNewHand();
    }

    // ========== WinnerText ==========
    private void ShowWinnersUI(List<Player> winners, List<CardData> board5, string suffix)
    {
        if (winnerText == null) return;

        if (winners == null || winners.Count == 0)
        {
            winnerText.text = "No Winner";
            winnerText.gameObject.SetActive(true);
            return;
        }

        string cat = "HighCard";
        if (board5 != null && board5.Count > 0)
        {
            var hole = winners[0].GetComponentsInChildren<Card>()
                                 .Select(c => c.cardData)
                                 .ToList();
            if (hole.Count >= 2)
            {
                var hv = HandEvaluator.EvaluateBestFromHoleAndBoard(hole, board5);
                cat = hv.Category.ToString();
            }
        }

        string names = string.Join(", ", winners.ConvertAll(w => w.name));
        winnerText.text = $"🏆 {names}\n({cat}){suffix}";
        winnerText.gameObject.SetActive(true);
    }

    private void HideWinnersUI()
    {
        if (winnerText != null)
            winnerText.gameObject.SetActive(false);
    }
}
