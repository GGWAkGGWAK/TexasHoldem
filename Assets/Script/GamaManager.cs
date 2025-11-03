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
    public Text winnerText;

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
        winnerText = canvas.Find("승자표시").GetComponent<Text>();
        winnerText.text = "";
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
        if (winnerText != null)
            winnerText.text = "";
        Debug.Log($"[RoundStart] {currentStreet}, first={turnOrder[currentIndex].name}, actorsToAct={actorsToAct}");
    }

    // 플레이어 액션 처리 (콜, 폴드, 레이즈 등)
    public void RegisterAction(Player actor, ActionType action, bool isRaise)
    {
        // 액션에 따른 라운드 진행 카운트 업데이트
        if (action == ActionType.Fold)
        {
            // 폴드는 해당 플레이어가 이번 라운드 더 이상 액션하지 않으므로 감소
            actorsToAct = Mathf.Max(actorsToAct - 1, 0);
            Debug.Log($"[Action] {actor.name} FOLD → actorsToAct={actorsToAct}");
        }
        else if (isRaise)
        {
            // 레이즈가 발생하면 마지막 어그레서 갱신 + 한 바퀴 더
            lastAggressorIndex = turnOrder.IndexOf(actor);
            actorsToAct = ActivePlayersCount() - 1; // 레이저 제외 나머지 모두 액션 필요
            Debug.Log($"[Action] {actor.name} RAISE → lastAggressorIndex={lastAggressorIndex}, actorsToAct reset={actorsToAct}");
        }
        else
        {
            // 일반 액션(Call/Check/Bet 등)
            actorsToAct = Mathf.Max(actorsToAct - 1, 0);
            Debug.Log($"[Action] {actor.name} {action} → actorsToAct={actorsToAct}");
        }

        // 올 폴드 감지: 폴드하지 않은(=canPlay==true) 플레이어가 1명 이하인지 확인
        int alive = 0;
        foreach (var p in turnOrder)
            if (p != null && p.canPlay) alive++;

        if (alive <= 1)
        {
            WinByAllFold();   // → 즉시 승자 지급/표시, 핸드 종료 처리
            return;
        }

        // 이번 스트리트에서 모두 액션을 마쳤으면 다음 스트리트로
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

        // 현재 턴 해제
        actor.isMyTurn = false;

        // 마지막 어그레서 인덱스 보정
        if (lastAggressorIndex >= 0)
        {
            if (removedIndex < lastAggressorIndex) lastAggressorIndex--;
            else if (removedIndex == lastAggressorIndex) lastAggressorIndex = -1;
        }

        // 턴오더에서 제거
        turnOrder.RemoveAt(removedIndex);

        // 남은 인원이 없으면 종료
        if (turnOrder.Count == 0)
        {
            Debug.Log("[Fold] 모든 플레이어 제거됨 → 핸드 종료(정산 필요)");
            return;
        }

        // ✅ 올 폴드 감지: 폴드하지 않은(=canPlay==true) 플레이어가 1명 이하인지 확인
        int alive = 0;
        foreach (var p in turnOrder)
            if (p != null && p.canPlay) alive++;

        if (alive <= 1)
        {
            WinByAllFold();   // → 즉시 승자 지급/표시, 핸드 종료 처리
            return;
        }

        // 다음 턴 후보 계산 (제거 위치 기준으로 순회)
        currentIndex = removedIndex % turnOrder.Count;

        for (int step = 0; step < turnOrder.Count; step++)
        {
            int next = (currentIndex + step) % turnOrder.Count;
            var cand = turnOrder[next];

            // 다음 턴으로 넘길 유효 후보: 폴드 안 했고 칩 > 0 (올인은 액션 제외)
            if (cand != null && cand.canPlay && cand.playerChip > 0)
            {
                // 모든 플레이어 턴 플래그 초기화
                foreach (var p in turnOrder)
                    if (p != null) p.isMyTurn = false;

                currentIndex = next;
                cand.isMyTurn = true;

                Debug.Log($"[Fold→NextTurn] {cand.name}");
                return;
            }
        }

        // 유효 후보를 못 찾으면(모두 올인 등) → 다음 스트리트로 진행될 수 있음
        Debug.Log("[Fold] 유효 플레이어 없음 → 다음 진행 판단 필요");
    }

    private void ResolveShowdown()
    {
        var deck = FindObjectOfType<Deck>();
        List<CardData> board5 = deck.GetBoardCardData();

        var activePlayers = new List<Player>();
        foreach (var p in turnOrder)
            if (p != null && p.canPlay) activePlayers.Add(p);

        // 메인 승자(공동우승 포함)
        var winners = WinnerEvaluator.DecideWinners(activePlayers, board5);

        // ✅ 화면 표시용 문자열 만들기 (족보까지)
        if (winnerText != null)
        {
            if (winners.Count == 0)
            {
                winnerText.text = "Winner: -";
            }
            else
            {
                var parts = new List<string>();
                foreach (var w in winners)
                {
                    // w의 홀카드로 족보 재평가(표시용)
                    var hole = w.GetComponentsInChildren<Card>(true);
                    var holeData = new List<CardData>();
                    foreach (var c in hole) holeData.Add(c.cardData);

                    var hv = HandEvaluator.EvaluateBestFromHoleAndBoard(holeData, board5);
                    parts.Add($"{w.name} ({hv.Category})");
                }
                winnerText.text = (winners.Count == 1)
                    ? $"Winner: {parts[0]}"
                    : $"Winners (Split): {string.Join(", ", parts)}";
            }
        }

        // 실제 분배(사이드팟 포함) – 이미 구현한 사이드팟 로직 사용 시:
        var allPlayers = new List<Player>(turnOrder);
        var potsList = SidePot.BuildPots(allPlayers);
        SidePot.DistributeAllPots(potsList, board5);

        pots = 0;
        foreach (var p in allPlayers) { if (p == null) continue; p.contributedThisHand = 0; p.isAllIn = false; }

        Debug.Log($"Showdown 완료. Winners: {winners.Count}");
    }
    // GamaManager.cs
    private void WinByAllFold()
    {
        // 폴드하지 않은 플레이어(=canPlay == true)만 찾기
        var alive = new List<Player>();
        foreach (var p in turnOrder)
            if (p != null && p.canPlay) alive.Add(p);

        if (alive.Count != 1) return; // 안전장치

        var winner = alive[0];

        // 팟 지급
        winner.playerChip += pots;
        int paid = pots;
        pots = 0;

        // UI 표시
        if (winnerText != null)
            winnerText.text = $"Winner: {winner.name} (+{paid:N0})";

        // 다음 핸드 초기화 준비 (원하면 여기서 셔플/리셋 호출)
        foreach (var p in turnOrder)
        {
            if (p == null) continue;
            p.contributedThisHand = 0;
            p.isAllIn = false;
            p.isMyTurn = false;
        }

        Debug.Log($"[AllFold] {winner.name} 승리. {paid:N0} 수령");
    }


}
