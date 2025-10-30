using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum Street { Preflop, Flop, Turn, River, Showdown }
public enum ActionType { Fold, Check, Call, Bet, Raise, AllIn }

public class GamaManager : MonoBehaviour
{
    public int smallBlind;             // 스몰블라인드
    public int BigBlind;               // 빅블라인드
    public float duration;             // 듀레이션
    public float bettingTime;          // 베팅 제한 시간
    public int pots;                   // 팟 금액
    public int beforeBettingChip;      // 직전 베팅 금액
    public int beforeRaiseChip;        // 베팅칩과 레이즈칩의 차액

    public Text potsText;              // 팟 표시 UI

    public List<Player> turnOrder = new List<Player>();
    public int currentIndex = -1;

    // 현재 스트리트
    public Street currentStreet = Street.Preflop;


    private int lastAggressorIndex = -1;  // 마지막으로 레이즈한 플레이어 인덱스
    private int actorsToAct = 0;          // 이번 스트리트에서 남은 플레이어 액션 수
    private bool waitingForRoundEnd = false;


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
        beforeBettingChip = BigBlind;

        yield return null;

        BuildTurnOrderBySeats();

        currentStreet = Street.Preflop;
        StartBettingRound(0);
    }

    void Update()
    {
        potsText.text = "Pots: " + pots.ToString("N0");
    }

    // 좌석 기준으로 턴 순서 구성
    public void BuildTurnOrderBySeats()
    {
        turnOrder.Clear();

        var seats = new List<Seat>(FindObjectsOfType<Seat>(true));
        seats.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        foreach (var seat in seats)
        {
            var p = seat.GetComponentInChildren<Player>(true);
            seat.isSeated = (p != null);
            if (p != null)
                turnOrder.Add(p);
        }

        foreach (var p in turnOrder)
            p.isMyTurn = false;

        Debug.Log($"[TurnOrder] 수집된 플레이어 수: {turnOrder.Count}");
    }

    public int ActivePlayersCount()
    {
        int cnt = 0;
        foreach (var p in turnOrder)
            if (p != null && p.canPlay && !p.isAllIn) // 칩보다는 올인 여부로 판정
                cnt++;
        return cnt;
    }

    // 새로운 베팅 라운드 시작
    public void StartBettingRound(int firstIndexToAct)
    {
        if (currentStreet != Street.Preflop)
        {
            beforeBettingChip = 0;
            beforeRaiseChip = 0;
        }

        foreach (var p in turnOrder)
            if (p != null) p.isMyTurn = false;

        currentIndex = Mathf.Clamp(firstIndexToAct, 0, turnOrder.Count - 1);
        turnOrder[currentIndex].isMyTurn = true;

        // 새 라운드 시작
        lastAggressorIndex = -1;
        actorsToAct = ActivePlayersCount();

        Debug.Log($"[RoundStart] {currentStreet}, first={turnOrder[currentIndex].name}, actorsToAct={actorsToAct}");
    }

    // 플레이어 액션 처리 (콜, 폴드, 레이즈 등)
    public void RegisterAction(Player actor, ActionType action, bool isRaise)
    {
        if (action == ActionType.Fold)
        {
            // 폴드 시 액션 수 감소
            actorsToAct = Mathf.Max(actorsToAct - 1, 0);
            Debug.Log($"[Action] {actor.name} FOLD → actorsToAct={actorsToAct}");
        }
        else if (isRaise)
        {
            // 레이즈 발생 시 다시 한 바퀴 돌아야 함
            lastAggressorIndex = turnOrder.IndexOf(actor);
            actorsToAct = ActivePlayersCount() - 1;

            Debug.Log($"[Action] {actor.name} RAISE → lastAggressorIndex={lastAggressorIndex}, actorsToAct reset={actorsToAct}");
        }
        else
        {
            // 일반 액션 (Call, Check 등)
            actorsToAct = Mathf.Max(actorsToAct - 1, 0);
            Debug.Log($"[Action] {actor.name} {action} → actorsToAct={actorsToAct}");
        }

        // 남은 플레이어가 한 명이라면 핸드 종료
        if (ActivePlayersCount() <= 1)
        {
            Debug.Log("[Hand] 남은 플레이어 1명 → 핸드 종료(정산 필요)");
            return;
        }

        // 모든 액션이 끝났다면 다음 스트리트로 이동
        if (actorsToAct == 0)
        {
            AdvanceStreet();
        }
    }

    // 다음 스트리트 진행
    public void AdvanceStreet()
    {
        var deck = FindObjectOfType<Deck>();

        switch (currentStreet)
        {
            case Street.Preflop:
                currentStreet = Street.Flop;
                deck.Plop();
                StartBettingRound(0);
                break;

            case Street.Flop:
                currentStreet = Street.Turn;
                deck.Turn();
                StartBettingRound(0);
                break;

            case Street.Turn:
                currentStreet = Street.River;
                deck.River();
                StartBettingRound(0);
                break;

            case Street.River:
                currentStreet = Street.Showdown;
                ResolveShowdown();
                break;
        }
    }

    // 다음 턴으로 넘기기
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
                Debug.Log($"[NextTurn] → {cand.name}");
                return;
            }
        }

        Debug.Log("[NextTurn] 유효 플레이어 없음");
    }

    // 폴드 처리 후 턴 이동 + 리스트 제거
    public void HandleFoldAndPassTurn(Player actor)
    {
        if (turnOrder.Count == 0) return;

        int removedIndex = turnOrder.IndexOf(actor);
        if (removedIndex < 0) removedIndex = currentIndex;

        actor.isMyTurn = false;

        // 어그레서가 접었는지 확인 후 보정
        if (lastAggressorIndex >= 0)
        {
            if (removedIndex < lastAggressorIndex) lastAggressorIndex--;
            else if (removedIndex == lastAggressorIndex) lastAggressorIndex = -1;
        }

        turnOrder.RemoveAt(removedIndex);

        if (turnOrder.Count == 0)
        {
            Debug.Log("[Fold] 모든 플레이어 제거됨 → 핸드 종료(정산 필요)");
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
                Debug.Log($"[Fold→NextTurn] {cand.name}");
                return;
            }
        }

        Debug.Log("[Fold] 유효 플레이어 없음 → 핸드 종료(정산 필요)");
    }
    private void ResolveShowdown()
{
    var deck = FindObjectOfType<Deck>();
    List<CardData> board5 = deck.GetBoardCardData();

    // 현재 참여 중인 플레이어만 수집 (너의 turnOrder 쓰면 더 정확)
    var activePlayers = new List<Player>();
    foreach (var p in turnOrder)
        if (p != null && p.canPlay) activePlayers.Add(p);

    // 공동 우승자 판정
    var winners = WinnerEvaluator.DecideWinners(activePlayers, board5);

    // 팟 분배
    WinnerEvaluator.DistributePot(pots, winners);
    pots = 0;

    // TODO: 우승자 하이라이트/토스트, 다음 핸드 초기화, 버튼 상태 리셋 등
    Debug.Log($"Showdown 완료. Winners: {winners.Count}");
}
}
