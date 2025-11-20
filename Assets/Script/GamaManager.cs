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
    public int bigBlind;
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
    public int buttonIndex = 9;
    public int sbIndex = 0;
    public int bbIndex = 1;

    [Header("Player Management")]
    public GameObject playerPrefab;

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
        bigBlind = 20000;
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

        //BeginNewHand();
    }

    private void Update()
    {
        if (potsText != null) potsText.text = "Pots: " + pots.ToString("N0");
    }
    
    // 새 플레이어 추가 메서드
    public void AddNewPlayerToEmptySeat()
    {
        // 빈 좌석 찾기
        Seat emptySeat = FindEmptySeat();

        if (emptySeat == null)
        {
            Debug.Log("💺 빈 좌석이 없습니다!");
            return;
        }

        // 새 플레이어 생성
        GameObject newPlayerObj = CreateNewPlayer(emptySeat);

        if (newPlayerObj != null)
        {
            Player newPlayer = newPlayerObj.GetComponent<Player>();
            Debug.Log($"🆕 새 플레이어가 {emptySeat.name}에 추가되었습니다! (칩: {newPlayer.playerChip:N0})");

            // 다음 핸드부터 참여하도록 좌석 상태 업데이트
            emptySeat.isSeated = true;
        }
    }

    // 빈 좌석 찾기
    private Seat FindEmptySeat()
    {
        foreach (var seat in seatOrder)
        {
            if (seat != null && !seat.isSeated)
            {
                // 해당 좌석에 플레이어가 없는지 확인
                Player existingPlayer = GetPlayerAtSeat(seat);
                if (existingPlayer == null)
                {
                    return seat;
                }
            }
        }
        return null;
    }

    // 새 플레이어 생성
    private GameObject CreateNewPlayer(Seat targetSeat)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("❌ playerPrefab이 설정되지 않았습니다!");
            return null;
        }

        // 플레이어 프리팹 인스턴스 생성
        GameObject newPlayerObj = Instantiate(playerPrefab, targetSeat.transform);

        // 플레이어 컴포넌트 설정
        Player newPlayer = newPlayerObj.GetComponent<Player>();
        if (newPlayer != null)
        {
            // 플레이어 기본 설정 (playerChip은 프리팹 기본값 사용)
            newPlayer.canPlay = true;
            newPlayer.isMyTurn = false;
            newPlayer.isAllIn = false;
            newPlayer.contributedThisHand = 0;
            newPlayer.contributedThisRound = 0;

            // 좌석 연결
            newPlayerObj.transform.SetParent(targetSeat.transform);
            newPlayerObj.transform.localPosition = Vector3.zero;
            newPlayerObj.transform.localRotation = Quaternion.identity;

            // playerChip 체크
            if (newPlayer.playerChip <= 0)
            {
                Debug.LogWarning($"⚠️ 새 플레이어의 시작 칩이 0입니다. Player 프리팹의 playerChip 값을 확인해주세요!");
            }
        }

        return newPlayerObj;
    }
    // ========== 플레이어 아웃 시스템 ==========

    /// 쇼다운 후 칩이 0인 플레이어들을 아웃시키는 처리
    private void ProcessEliminatedPlayers()
    {
        List<Player> eliminatedPlayers = new List<Player>();

        foreach (var player in turnOrder.ToList())
        {
            if (player != null && player.playerChip <= 0)
            {
                eliminatedPlayers.Add(player);
            }
        }

        foreach (var player in eliminatedPlayers)
        {
            EliminatePlayer(player);
        }
    }

    /// 개별 플레이어 제거 처리
    private void EliminatePlayer(Player player)
    {
        if (player == null) return;

        Debug.Log($"🚨 {player.name}이(가) 아웃되었습니다!");

        RemovePlayerFromSeat(player);
        RemovePlayerFromTurnOrder(player);
        RecalculateIndicesAfterElimination();
    }

    /// 좌석에서 플레이어 제거
    private void RemovePlayerFromSeat(Player player)
    {
        if (player == null) return;

        var seat = seatOrder.FirstOrDefault(s => s != null && GetPlayerAtSeat(s) == player);
        if (seat != null)
        {
            seat.isSeated = false;
            Destroy(player.gameObject); // 플레이어 오브젝트 완전 삭제
        }
    }

    /// 턴 순서에서 플레이어 제거
    private void RemovePlayerFromTurnOrder(Player player)
    {
        if (turnOrder.Contains(player))
        {
            int playerTurnIndex = turnOrder.IndexOf(player);
            turnOrder.Remove(player);

            if (currentIndex > playerTurnIndex)
            {
                currentIndex--;
            }
            else if (currentIndex == playerTurnIndex && currentIndex >= turnOrder.Count)
            {
                currentIndex = turnOrder.Count > 0 ? 0 : -1;
            }
        }
    }

    /// 제거 후 인덱스 재조정
    private void RecalculateIndicesAfterElimination()
    {
        BuildSeatOrder();
        BuildTurnOrderBySeats();

        if (seatOrder.Count == 0 || turnOrder.Count == 0) return;

        buttonIndex = Mathf.Clamp(buttonIndex, 0, seatOrder.Count - 1);

        if (GetPlayerAtSeat(seatOrder[buttonIndex]) == null)
        {
            buttonIndex = FindFirstActivePlayerSeat();
        }

        if (turnOrder.Count >= 2)
        {
            sbIndex = NextSeatWithPlayerFrom(buttonIndex);
            bbIndex = NextSeatWithPlayerFrom(sbIndex);
        }
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

    public void BuildTurnOrderBySeats()
    {
        turnOrder.Clear();
        foreach (var seat in seatOrder)
        {
            var p = GetPlayerAtSeat(seat);
            if (p != null) turnOrder.Add(p);
        }
        foreach (var p in turnOrder) if (p != null) p.isMyTurn = false;
    }

    private int TurnIndexFromSeatIndex(int seatIdx)
    {
        if (seatIdx < 0 || seatIdx >= seatOrder.Count) return 0;

        var seat = seatOrder[seatIdx];
        var player = GetPlayerAtSeat(seat);

        if (player == null) return 0;

        int turnIdx = turnOrder.IndexOf(player);
        return (turnIdx >= 0) ? turnIdx : 0;
    }

    // ========== 버튼 이동 ==========
    private void AdvanceButtonToNextPlayer()
    {
        if (turnOrder.Count <= 1) return;

        // 현재 버튼 플레이어 찾기
        var currentButtonPlayer = GetPlayerAtSeat(seatOrder[buttonIndex]);

        if (currentButtonPlayer == null)
        {
            // 첫 번째 활성 플레이어를 버튼으로 설정
            buttonIndex = FindFirstActivePlayerSeat();
        }
        else
        {
            // turnOrder에서 다음 플레이어 찾기
            int currentTurnIndex = turnOrder.IndexOf(currentButtonPlayer);
            int nextTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
            var nextButtonPlayer = turnOrder[nextTurnIndex];

            // 다음 플레이어의 좌석 인덱스 찾기
            bool foundSeat = false;
            for (int i = 0; i < seatOrder.Count; i++)
            {
                if (GetPlayerAtSeat(seatOrder[i]) == nextButtonPlayer)
                {
                    buttonIndex = i;
                    foundSeat = true;
                    break;
                }
            }

            if (!foundSeat) buttonIndex = FindFirstActivePlayerSeat();
        }

        // SB/BB 설정 (게임 진행용 메서드 사용 - canPlay 체크함)
        sbIndex = NextSeatWithPlayerFrom(buttonIndex);
        if (sbIndex < 0) return;

        bbIndex = NextSeatWithPlayerFrom(sbIndex);
        if (bbIndex < 0) return;
    }

    private int FindFirstActivePlayerSeat()
    {
        for (int i = 0; i < seatOrder.Count; i++)
        {
            var player = GetPlayerAtSeat(seatOrder[i]);
            if (player != null && player.canPlay && player.playerChip > 0)
                return i;
        }
        return 0; // fallback
    }

    private int NextSeatWithPlayerFrom(int from)
    {
        if (seatOrder.Count == 0) return -1;

        int tries = 0;
        int idx = (from + 1) % seatOrder.Count;

        while (tries < seatOrder.Count)
        {
            if (idx >= 0 && idx < seatOrder.Count)
            {
                var seat = seatOrder[idx];
                var player = GetPlayerAtSeat(seat);

                // 게임 진행용: canPlay 체크
                if (player != null && player.canPlay && player.playerChip > 0)
                    return idx;
            }

            idx = (idx + 1) % seatOrder.Count;
            tries++;
        }
        return -1;
    }

    private void TeleportDealerButton()
    {
        if (dealerButton == null) return;

        var player = (buttonIndex >= 0 && buttonIndex < turnOrder.Count) ? turnOrder[buttonIndex] : null;
        if (player != null && player.gameObject != null)
        {
            dealerButton.TeleportTo(player.transform);
        }
    }
    private void MoveDealerButton()
    {
        if (dealerButton == null) return;

        // buttonIndex를 turnOrder 기준으로 변환
        var player = (buttonIndex >= 0 && buttonIndex < turnOrder.Count) ? turnOrder[buttonIndex] : null;
        if (player != null && player.gameObject != null)
        {
            dealerButton.MoveTo(player.transform);
        }
    }
    // 다음 핸드로 진행하기 전 버튼 이동
    private void MoveButtonToNextPlayer()
    {
        if (turnOrder.Count < 2)
        {
            Debug.LogError("❌ 버튼 이동 불가: 플레이어가 2명 미만입니다!");
            return;
        }

        // 버튼을 한 자리만 이동
        buttonIndex = (buttonIndex + 1) % turnOrder.Count;

        // 다음 플레이어가 활성(칩 > 0)인지 확인
        if (turnOrder[buttonIndex].playerChip > 0)
        {
            Debug.Log($"[MoveButtonToNextPlayer] 버튼 이동 → turnOrder Index: {buttonIndex}");
        }
        else
        {
            // 탈락한 플레이어면 다음을 찾음
            buttonIndex = FindNextActivePlayerIndex(buttonIndex);
            Debug.Log($"[MoveButtonToNextPlayer] 탈락자 건너뛰고 → turnOrder Index: {buttonIndex}");
        }
    }
    // 다음 활성 플레이어 찾기
    private int FindNextActivePlayerIndex(int startIndex)
    {
        int searchIndex = (startIndex + 1) % turnOrder.Count;

        // 한 번만 체크
        if (turnOrder[searchIndex] != null && turnOrder[searchIndex].playerChip > 0)
        {
            return searchIndex;
        }

        // 그 다음도 없으면 처음부터 찾기
        for (int i = 0; i < turnOrder.Count; i++)
        {
            if (turnOrder[i] != null && turnOrder[i].playerChip > 0)
            {
                return i;
            }
        }

        Debug.LogError("❌ 활성 플레이어를 찾을 수 없습니다!");
        return 0;
    }
    // ========== 새 핸드 ==========
    public void BeginNewHand()
    {
        HideWinnersUI();

        var deck = FindObjectOfType<Deck>();
        if (deck == null) return;

        pots = 0;
        beforeBettingChip = 0;
        currentStreet = Street.Preflop;

        // 새로 추가된 플레이어들 포함하여 좌석/턴 순서 재스캔
        BuildSeatOrder();
        BuildTurnOrderBySeats();

        // 디버그: 현재 상태 출력
        Debug.Log($"[BeginNewHand] seatOrder.Count: {seatOrder?.Count ?? 0}, turnOrder.Count: {turnOrder?.Count ?? 0}");

        // 최소 플레이어 수 체크
        if (turnOrder == null || turnOrder.Count < 2)
        {
            Debug.LogError($"[BeginNewHand] ❌ 게임을 시작할 수 없습니다! 플레이어 수: {turnOrder?.Count ?? 0}");
            return;
        }

        // buttonIndex를 turnOrder 기준으로 조정
        if (buttonIndex < 0 || buttonIndex >= turnOrder.Count)
        {
            buttonIndex = 0;
        }

        // Modulo 연산으로 SB, BB 인덱스 계산 (안전함)
        sbIndex = (buttonIndex + 1) % turnOrder.Count;
        bbIndex = (buttonIndex + 2) % turnOrder.Count;

        Debug.Log($"[BeginNewHand] buttonIndex: {buttonIndex}, sbIndex: {sbIndex}, bbIndex: {bbIndex}, turnOrder.Count: {turnOrder.Count}");

        TeleportDealerButton();

        // 플레이어 상태 초기화 및 게임 종료 조건 체크
        int playersWithChips = 0;
        Player winner = null;

        for (int i = 0; i < turnOrder.Count; i++)
        {
            var p = turnOrder[i];
            if (p == null) continue;

            bool oldCanPlay = p.canPlay;
            p.isMyTurn = false;
            p.canPlay = (p.playerChip > 0);
            p.isAllIn = false;
            p.contributedThisHand = 0;

            Debug.Log($"[BeginNewHand] {p.name}: chips={p.playerChip}, canPlay={oldCanPlay}→{p.canPlay}");

            if (p.playerChip > 0)
            {
                playersWithChips++;
                winner = p;
            }
        }

        // 게임 종료 조건 체크
        if (playersWithChips <= 1)
        {
            Debug.Log($"[BeginNewHand] 🏆 게임 종료! 최종 승자: {winner?.name ?? "없음"}");
            if (winner != null)
            {
                ShowWinnersUI(new List<Player> { winner }, null,
                    suffix: $"\n🎉 TOURNAMENT CHAMPION! 🎉\n총 상금: {winner.playerChip:N0}");
            }
            return;
        }

        deck.ShuffleDeck();
        PostBlinds(); // 이제 안전하게 호출

        var order = BuildPreflopDealingOrder();
        Debug.Log($"[BeginNewHand] 카드 배분 대상: {order?.Count ?? 0}명");

        if (order == null || order.Count < 2)
        {
            Debug.LogError($"[BeginNewHand] ❌ 카드 배분 대상이 부족합니다! ({order?.Count ?? 0}명)");
            return;
        }

        deck.PreflopDealInOrder(order);

        int utgSeatIdx = FirstToActPreflopSeatIndex();
        int utgTurnIdx = TurnIndexFromSeatIndex(utgSeatIdx);
        StartBettingRound(utgTurnIdx);
    }

    private List<Seat> BuildPreflopDealingOrder()
    {
        var order = new List<Seat>();

        // turnOrder 순서대로 칩 있는 플레이어만 추가
        for (int i = 0; i < turnOrder.Count; i++)
        {
            var player = turnOrder[i];
            if (player != null && player.playerChip > 0)
            {
                // 해당 플레이어의 좌석 찾기
                for (int seatIdx = 0; seatIdx < seatOrder.Count; seatIdx++)
                {
                    if (GetPlayerAtSeat(seatOrder[seatIdx]) == player)
                    {
                        order.Add(seatOrder[seatIdx]);
                        break;
                    }
                }
            }
        }
        return order;
    }

    private void PostBlinds()
    {
        // 디버그: 현재 상태 확인
        Debug.Log($"[PostBlinds] turnOrder.Count: {turnOrder?.Count ?? 0}, sbIndex: {sbIndex}, bbIndex: {bbIndex}");

        // 안전성 체크
        if (turnOrder == null || turnOrder.Count == 0)
        {
            Debug.LogError("[PostBlinds] ❌ turnOrder가 비어있습니다!");
            return;
        }

        if (turnOrder.Count < 2)
        {
            Debug.LogError($"[PostBlinds] ❌ 플레이어가 부족합니다! ({turnOrder.Count}명)");
            return;
        }

        // 🆕 buttonIndex 유효성 체크
        if (buttonIndex < 0 || buttonIndex >= turnOrder.Count)
        {
            Debug.LogWarning($"[PostBlinds] ⚠️ buttonIndex({buttonIndex})가 범위를 벗어남. 0으로 재설정합니다.");
            buttonIndex = 0;
        }

        // 인덱스 유효성 체크 및 수정 (modulo 연산 사용)
        sbIndex = (buttonIndex + 1) % turnOrder.Count;
        bbIndex = (buttonIndex + 2) % turnOrder.Count;

        Debug.Log($"[PostBlinds] 재계산됨 - buttonIndex: {buttonIndex}, sbIndex: {sbIndex}, bbIndex: {bbIndex}");

        // 최종 안전성 체크
        if (sbIndex < 0 || sbIndex >= turnOrder.Count || bbIndex < 0 || bbIndex >= turnOrder.Count)
        {
            Debug.LogError($"[PostBlinds] ❌ 인덱스 오류! sbIndex: {sbIndex}, bbIndex: {bbIndex}, turnOrder.Count: {turnOrder.Count}");
            return;
        }

        // 블라인드 처리
        var sbPlayer = turnOrder[sbIndex];
        var bbPlayer = turnOrder[bbIndex];

        if (sbPlayer == null || bbPlayer == null)
        {
            Debug.LogError("[PostBlinds] ❌ SB 또는 BB 플레이어가 null입니다!");
            return;
        }

        // 실제 블라인드 베팅 처리
        int actualSB = Mathf.Min(smallBlind, sbPlayer.playerChip);
        int actualBB = Mathf.Min(bigBlind, bbPlayer.playerChip);

        sbPlayer.playerChip -= actualSB;
        sbPlayer.contributedThisHand = actualSB;
        sbPlayer.contributedThisRound = actualSB;

        bbPlayer.playerChip -= actualBB;
        bbPlayer.contributedThisHand = actualBB;
        bbPlayer.contributedThisRound = actualBB;

        pots += actualSB + actualBB;

        beforeBettingChip = actualBB;

        Debug.Log($"[PostBlinds] SB: {sbPlayer.name}({actualSB:N0}), BB: {bbPlayer.name}({actualBB:N0})");
        Debug.Log($"[PostBlinds] beforeBettingChip 설정: {beforeBettingChip:N0}, 현재 팟: {pots:N0}");
    }

    // ========== 액션 시작자 ==========
    private int FirstToActPreflopSeatIndex()
    {
        int idx = bbIndex;
        for (int i = 0; i < seatOrder.Count; i++)
        {
            idx = NextSeatWithPlayerFrom(idx);
            var p = (idx >= 0) ? GetPlayerAtSeat(seatOrder[idx]) : null;
            if (p != null && p.canPlay) return idx;
        }
        return bbIndex;
    }

    private int FirstToActPostflopSeatIndex()
    {
        if (seatOrder.Count == 0) return -1;

        int currentSeat = buttonIndex;

        for (int i = 0; i < seatOrder.Count; i++)
        {
            currentSeat = (currentSeat + 1) % seatOrder.Count;

            if (currentSeat >= 0 && currentSeat < seatOrder.Count)
            {
                var seat = seatOrder[currentSeat];
                var player = GetPlayerAtSeat(seat);

                if (player != null && player.canPlay)
                    return currentSeat;
            }
        }
        return buttonIndex;
    }

    // ========== 베팅 라운드 ==========
    public void StartBettingRound(int firstTurnIndex)
    {
        if (currentStreet != Street.Preflop)
        {
            beforeBettingChip = 0;
        }

        // 새 베팅 라운드 시작 시 모든 플레이어의 라운드별 기여금 초기화
        foreach (var player in turnOrder)
        {
            if (player != null)
                player.contributedThisRound = 0;
        }

        // 프리플랍인 경우만 블라인드 플레이어들의 라운드별 기여금 설정
        if (currentStreet == Street.Preflop)
        {
            var sbPlayer = GetPlayerAtSeat(seatOrder[sbIndex]);
            var bbPlayer = GetPlayerAtSeat(seatOrder[bbIndex]);

            if (sbPlayer != null)
                sbPlayer.contributedThisRound = smallBlind;

            if (bbPlayer != null)
                bbPlayer.contributedThisRound = bigBlind;
        }

        foreach (var p in turnOrder)
            if (p != null) p.isMyTurn = false;

        currentIndex = Mathf.Clamp(firstTurnIndex, 0, turnOrder.Count - 1);

        if (turnOrder.Count > 0 && currentIndex >= 0 && currentIndex < turnOrder.Count)
        {
            var targetPlayer = turnOrder[currentIndex];
            if (targetPlayer != null)
            {
                targetPlayer.isMyTurn = true;

                for (int i = 0; i < turnOrder.Count; i++)
                {
                    if (i != currentIndex && turnOrder[i] != null)
                        turnOrder[i].isMyTurn = false;
                }
            }
        }

        lastAggressorIndex = -1;
        actorsToAct = ActivePlayersCount();
    }

    public int ActivePlayersCount()
    {
        int cnt = 0;
        foreach (var p in turnOrder)
        {
            if (p != null && p.canPlay)
                cnt++;
        }
        return cnt;
    }

    public int PlayersWithChipsCount()
    {
        int cnt = 0;
        foreach (var p in turnOrder)
        {
            if (p != null && p.canPlay && p.playerChip > 0)
                cnt++;
        }
        return cnt;
    }

    public void RegisterAction(Player actor, ActionType action, bool isRaise, int raisedAmount = 0)
    {
        if (action == ActionType.Fold)
            actorsToAct = Mathf.Max(actorsToAct - 1, 0);
        else if (isRaise)
        {
            lastAggressorIndex = turnOrder.IndexOf(actor);
            actorsToAct = ActivePlayersCount() - 1;
            beforeBettingChip = raisedAmount;
        }
        else
            actorsToAct = Mathf.Max(actorsToAct - 1, 0);

        if (ActivePlayersCount() <= 1)
        {
            WinByAllFold();
            return;
        }

        int playersWithChips = PlayersWithChipsCount();

        if (playersWithChips == 0)
        {
            actorsToAct = 0;
        }

        if (actorsToAct == 0)
        {
            AdvanceStreet();
            return;
        }
    }

    public void NextTurnFrom(Player actor)
    {
        if (turnOrder.Count == 0) return;

        int idx = turnOrder.IndexOf(actor);
        if (idx < 0) idx = currentIndex;

        actor.isMyTurn = false;

        // 포스트플랍에서는 좌석 순서 강제 적용
        if (currentStreet != Street.Preflop)
        {
            int currentSeatIdx = -1;
            for (int i = 0; i < seatOrder.Count; i++)
            {
                if (GetPlayerAtSeat(seatOrder[i]) == actor)
                {
                    currentSeatIdx = i;
                    break;
                }
            }

            if (currentSeatIdx >= 0)
            {
                for (int step = 1; step <= seatOrder.Count; step++)
                {
                    int nextSeatIdx = (currentSeatIdx + step) % seatOrder.Count;
                    var nextPlayer = GetPlayerAtSeat(seatOrder[nextSeatIdx]);

                    if (nextPlayer != null && nextPlayer.canPlay && nextPlayer.playerChip > 0)
                    {
                        int nextTurnIdx = turnOrder.IndexOf(nextPlayer);
                        if (nextTurnIdx >= 0)
                        {
                            currentIndex = nextTurnIdx;
                            nextPlayer.isMyTurn = true;
                            return;
                        }
                    }
                }
            }
        }

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

        int playersWithChips = PlayersWithChipsCount();

        bool isAllInSituation = playersWithChips == 0;

        if (isAllInSituation)
        {
            HandleAllInSituation(deck);
            return;
        }

        // 일반적인 스트리트 진행
        switch (currentStreet)
        {
            case Street.Preflop:
                currentStreet = Street.Flop;
                deck.Plop();
                StartNextBettingRound();
                break;

            case Street.Flop:
                currentStreet = Street.Turn;
                deck.Turn();
                StartNextBettingRound();
                break;

            case Street.Turn:
                currentStreet = Street.River;
                deck.River();
                StartNextBettingRound();
                break;

            case Street.River:
                ResolveShowdown();
                break;

            default:
                break;
        }
    }

    private void HandleAllInSituation(Deck deck)
    {
        switch (currentStreet)
        {
            case Street.Preflop:
                deck.Plop();
                deck.Turn();
                deck.River();
                currentStreet = Street.River;
                break;

            case Street.Flop:
                deck.Turn();
                deck.River();
                currentStreet = Street.River;
                break;

            case Street.Turn:
                deck.River();
                currentStreet = Street.River;
                break;

            case Street.River:
                break;
        }
        ResolveShowdown();
    }

    private void StartNextBettingRound()
    {
        foreach (var p in turnOrder)
            if (p != null) p.isMyTurn = false;

        int firstIdx = TurnIndexFromSeatIndex(FirstToActPostflopSeatIndex());
        StartBettingRound(firstIdx);

        if (firstIdx >= 0 && firstIdx < turnOrder.Count)
        {
            var targetPlayer = turnOrder[firstIdx];
            if (targetPlayer != null)
            {
                foreach (var p in turnOrder)
                    if (p != null) p.isMyTurn = false;

                targetPlayer.isMyTurn = true;
                currentIndex = firstIdx;
            }
        }
    }

    private bool IsAllInSituation()
    {
        int playersWithChips = 0;
        int totalActivePlayers = 0;

        foreach (var player in turnOrder)
        {
            if (player != null && player.canPlay)
            {
                totalActivePlayers++;
                if (player.playerChip > 0)
                {
                    playersWithChips++;
                }
            }
        }

        return totalActivePlayers >= 2 && playersWithChips <= 1;
    }

    // ========== 쇼다운/올폴드 ==========
    private void ResolveShowdown()
    {
        var deck = FindObjectOfType<Deck>();
        List<CardData> board5 = deck.GetBoardCardData();

        if (board5 == null || board5.Count == 0) return;

        var activePlayers = new List<Player>();
        foreach (var p in turnOrder)
        {
            if (p != null && p.canPlay)
                activePlayers.Add(p);
        }

        if (activePlayers.Count == 0) return;

        var allPots = SidePot.BuildPots(turnOrder);
        string potsSummary = SidePot.DistributeAllPots(allPots, board5);
        this.pots = 0;

        var winners = WinnerEvaluator.DecideWinners(activePlayers, board5);
        if (winners == null) winners = new List<Player>();

        ShowWinnersUI(winners, board5, suffix: "\n" + potsSummary);

        // 🆕 아웃된 플레이어 처리 추가
        ProcessEliminatedPlayers();

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

        // 🆕 아웃된 플레이어 처리 추가
        ProcessEliminatedPlayers();

        StartCoroutine(Co_NextHandAfterDelay(nextHandDelay));
    }

    private IEnumerator Co_NextHandAfterDelay(float sec)
    {
        yield return new WaitForSeconds(sec);

        BuildSeatOrder();
        AdvanceButtonToNextPlayer();
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

        string cat = "";

        if (board5 != null && board5.Count > 0)
        {
            var hole = winners[0].GetComponentsInChildren<Card>()
                                 .Select(c => c.cardData)
                                 .ToList();
            if (hole.Count >= 2)
            {
                var hv = HandEvaluator.EvaluateBestFromHoleAndBoard(hole, board5);
                cat = $"\n({hv.Category})";
            }
        }

        string names = string.Join(", ", winners.ConvertAll(w => w.name));
        winnerText.text = $"🏆 {names}{cat}{suffix}";
        winnerText.gameObject.SetActive(true);
    }

    private void HideWinnersUI()
    {
        if (winnerText != null)
            winnerText.gameObject.SetActive(false);
    }
}