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
    private bool SeatHasPlayer(Seat s)
    {
        if (s == null) return false;
        var player = GetPlayerAtSeat(s);
        // 더 엄격한 조건: 플레이어가 존재하고, 게임 참여 가능하고, 칩이 있어야 함
        return player != null && player.canPlay && player.playerChip > 0;
    }

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
        Debug.Log($"[TurnIndexFromSeatIndex] Input seatIdx: {seatIdx}");

        if (seatIdx < 0 || seatIdx >= seatOrder.Count)
        {
            Debug.LogWarning($"[TurnIndexFromSeatIndex] Invalid seatIdx: {seatIdx}, seatOrder.Count: {seatOrder.Count}");
            return 0;
        }

        var seat = seatOrder[seatIdx];
        var player = GetPlayerAtSeat(seat);

        Debug.Log($"[TurnIndexFromSeatIndex] seatOrder[{seatIdx}] = {seat}");
        Debug.Log($"[TurnIndexFromSeatIndex] GetPlayerAtSeat({seat}) = {(player != null ? player.name : "null")}");

        if (player == null)
        {
            Debug.LogWarning($"[TurnIndexFromSeatIndex] No player at seat #{seatIdx + 1}");
            return 0;
        }

        int turnIdx = turnOrder.IndexOf(player);

        // 중요한 디버깅 정보
        Debug.Log($"[TurnIndexFromSeatIndex] ✅ Seat #{seatIdx + 1} → Player: {player.name} → TurnIndex: {turnIdx}");
        Debug.Log($"[TurnIndexFromSeatIndex] TurnOrder: [{string.Join(", ", turnOrder.ConvertAll(p => p ? p.name : "null"))}]");
        Debug.Log($"[TurnIndexFromSeatIndex] Expected result: Player {player.name} should be first to act");

        return (turnIdx >= 0) ? turnIdx : 0;
    }

    // ========== 버튼 이동 ==========
    // 다음 핸드로 넘어갈 때 버튼은 "무조건 한 칸" 이동(비었어도 건너뛰지 않음)
    private void AdvanceButtonToNextPlayer()
    {
        if (turnOrder.Count <= 1)
        {
            Debug.LogError("[AdvanceButton] Not enough players for button rotation!");
            return;
        }

        // 🎯 현재 버튼 플레이어 찾기
        var currentButtonPlayer = GetPlayerAtSeat(seatOrder[buttonIndex]);

        if (currentButtonPlayer == null)
        {
            Debug.LogError($"[AdvanceButton] No player at current button position (seat #{buttonIndex + 1})");
            // 첫 번째 활성 플레이어를 버튼으로 설정
            buttonIndex = FindFirstActivePlayerSeat();
            Debug.Log($"[AdvanceButton] Reset button to first active player at seat #{buttonIndex + 1}");
        }
        else
        {
            // 🎯 turnOrder에서 다음 플레이어 찾기
            int currentTurnIndex = turnOrder.IndexOf(currentButtonPlayer);
            int nextTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
            var nextButtonPlayer = turnOrder[nextTurnIndex];

            Debug.Log($"[AdvanceButton] Current button: {currentButtonPlayer.name}, Next: {nextButtonPlayer.name}");

            // 🎯 다음 플레이어의 좌석 인덱스 찾기
            bool foundSeat = false;
            for (int i = 0; i < seatOrder.Count; i++)
            {
                if (GetPlayerAtSeat(seatOrder[i]) == nextButtonPlayer)
                {
                    buttonIndex = i;
                    foundSeat = true;
                    Debug.Log($"[AdvanceButton] Found {nextButtonPlayer.name} at seat #{i + 1}");
                    break;
                }
            }

            if (!foundSeat)
            {
                Debug.LogError($"[AdvanceButton] Could not find seat for {nextButtonPlayer.name}!");
                buttonIndex = FindFirstActivePlayerSeat();
            }
        }

        // 🎯 SB/BB 설정 (게임 진행용 메서드 사용 - canPlay 체크함)
        sbIndex = NextSeatWithPlayerFrom(buttonIndex);
        if (sbIndex < 0)
        {
            Debug.LogError("[AdvanceButton] Cannot find Small Blind player!");
            return;
        }

        bbIndex = NextSeatWithPlayerFrom(sbIndex);
        if (bbIndex < 0)
        {
            Debug.LogError("[AdvanceButton] Cannot find Big Blind player!");
            return;
        }

        var btnPlayer = GetPlayerAtSeat(seatOrder[buttonIndex]);
        var sbPlayer = GetPlayerAtSeat(seatOrder[sbIndex]);
        var bbPlayer = GetPlayerAtSeat(seatOrder[bbIndex]);

        Debug.Log($"[Button] BTN: {btnPlayer?.name} (seat #{buttonIndex + 1}), SB: {sbPlayer?.name} (seat #{sbIndex + 1}), BB: {bbPlayer?.name} (seat #{bbIndex + 1})");
    }

    private int FindFirstActivePlayerSeat()
    {
        Debug.Log("[FindFirstActivePlayerSeat] Searching for first active player...");

        for (int i = 0; i < seatOrder.Count; i++)
        {
            var player = GetPlayerAtSeat(seatOrder[i]);
            Debug.Log($"[FindFirstActivePlayerSeat] Seat #{i + 1}: Player = {player?.name}, CanPlay = {player?.canPlay}, Chips = {player?.playerChip}");

            if (player != null && player.canPlay && player.playerChip > 0)
            {
                Debug.Log($"[FindFirstActivePlayerSeat] ✅ Found first active player: {player.name} at seat #{i + 1}");
                return i;
            }
        }

        Debug.LogWarning("[FindFirstActivePlayerSeat] No active players found, returning 0");
        return 0; // fallback
    }
    private void AdvanceButtonOneSeat()
    {
        if (seatOrder.Count == 0)
        {
            Debug.LogError("[AdvanceButton] No seats available!");
            return;
        }

        // 현재 버튼 위치에서 다음 플레이어가 있는 자리로 이동
        int newButtonIndex = NextSeatWithPlayerFrom(buttonIndex);

        if (newButtonIndex < 0)
        {
            Debug.LogError("[AdvanceButton] Cannot find next player for button!");
            // 안전장치: 현재 위치 유지하거나 게임 종료
            return;
        }

        buttonIndex = newButtonIndex;

        // 버튼이 플레이어가 있는 자리에 있으므로 SB/BB 찾기가 안전해짐
        sbIndex = NextSeatWithPlayerFrom(buttonIndex);
        if (sbIndex < 0)
        {
            Debug.LogError("[AdvanceButton] Cannot find Small Blind player!");
            return;
        }

        bbIndex = NextSeatWithPlayerFrom(sbIndex);
        if (bbIndex < 0)
        {
            Debug.LogError("[AdvanceButton] Cannot find Big Blind player!");
            return;
        }

        Debug.Log($"[Button] BTN moved to seat #{buttonIndex + 1} (Player: {GetPlayerAtSeat(seatOrder[buttonIndex])?.name}), SB={sbIndex + 1}, BB={bbIndex + 1}");
    }

    // from 다음 자리부터, Player가 있는 좌석을 찾는다
    private int NextSeatWithPlayerFrom(int from)
    {
        if (seatOrder.Count == 0)
        {
            Debug.LogError("[NextSeat] seatOrder is empty!");
            return -1;
        }

        int tries = 0;
        int idx = (from + 1) % seatOrder.Count;

        Debug.Log($"[NextSeat] Starting search from seat #{from + 1}, first check at #{idx + 1}");

        while (tries < seatOrder.Count)
        {
            if (idx >= 0 && idx < seatOrder.Count)
            {
                var seat = seatOrder[idx];
                var player = GetPlayerAtSeat(seat);

                Debug.Log($"[NextSeat] Checking seat #{idx + 1}: HasSeat={seat != null}, HasPlayer={player != null}, CanPlay={player?.canPlay ?? false}, HasChips={player?.playerChip > 0}");

                // 게임 진행용: canPlay 체크
                if (player != null && player.canPlay && player.playerChip > 0)
                {
                    Debug.Log($"[NextSeat] ✅ Found valid player at seat #{idx + 1}: {player.name}");
                    return idx;
                }
            }

            idx = (idx + 1) % seatOrder.Count;
            tries++;
        }

        Debug.LogError($"[NextSeat] ❌ No valid player found from seat #{from + 1} after {tries} attempts");
        return -1;
    }
    private int NextSeatWithPlayerForDealing(int from)
    {
        if (seatOrder.Count == 0)
        {
            Debug.LogError("[NextSeatForDealing] seatOrder is empty!");
            return -1;
        }

        int tries = 0;
        int idx = (from + 1) % seatOrder.Count;

        Debug.Log($"[NextSeatForDealing] Starting search from seat #{from + 1}");

        while (tries < seatOrder.Count)
        {
            if (idx >= 0 && idx < seatOrder.Count)
            {
                var seat = seatOrder[idx];
                var player = GetPlayerAtSeat(seat);

                Debug.Log($"[NextSeatForDealing] Checking seat #{idx + 1}: HasPlayer={player != null}, HasChips={player?.playerChip > 0}");

                // 중요: canPlay 체크 안 함! 칩만 있으면 카드 배분
                if (player != null && player.playerChip > 0)
                {
                    Debug.Log($"[NextSeatForDealing] ✅ Found player for dealing at seat #{idx + 1}: {player.name}");
                    return idx;
                }
            }

            idx = (idx + 1) % seatOrder.Count;
            tries++;
        }

        Debug.LogError($"[NextSeatForDealing] ❌ No player found for dealing from seat #{from + 1}");
        return -1;
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
        Debug.Log("=== BeginNewHand START ===");

        HideWinnersUI();

        var deck = FindObjectOfType<Deck>();
        if (deck == null)
        {
            Debug.LogError("[BeginNewHand] Deck not found!");
            return;
        }

        pots = 0;
        beforeBettingChip = 0;
        beforeRaiseChip = 0;
        currentStreet = Street.Preflop;

        Debug.Log("[BeginNewHand] 1단계: BuildTurnOrderBySeats() 호출 전");

        // 1단계: 매 핸드 시작마다 좌석 기준으로 턴오더 재구성
        BuildTurnOrderBySeats();

        Debug.Log($"[BeginNewHand] turnOrder 재구성 완료: {turnOrder.Count}명");
        for (int i = 0; i < turnOrder.Count; i++)
        {
            var p = turnOrder[i];
            if (p != null)
            {
                Debug.Log($"[BeginNewHand] turnOrder[{i}]: {p.name}, chips={p.playerChip}, canPlay={p.canPlay} (초기화 전)");
            }
        }

        Debug.Log("[BeginNewHand] 2단계: 플레이어 상태 초기화 시작");

        // 2단계: 플레이어 상태 초기화 (canPlay 포함)
        for (int i = 0; i < turnOrder.Count; i++)
        {
            var p = turnOrder[i];
            if (p == null) continue;

            Debug.Log($"[BeginNewHand] 초기화 전 - {p.name}: canPlay={p.canPlay}, chips={p.playerChip}");

            p.isMyTurn = false;
            p.canPlay = (p.playerChip > 0);  // 중요: 여기서 canPlay 초기화!
            p.isAllIn = false;
            p.contributedThisHand = 0;

            Debug.Log($"[BeginNewHand] 초기화 후 - {p.name}: canPlay={p.canPlay}, chips={p.playerChip}");
        }

        Debug.Log("[BeginNewHand] 3단계: PostBlinds() 호출 전");
        deck.ShuffleDeck();
        PostBlinds();

        Debug.Log("[BeginNewHand] 4단계: BuildPreflopDealingOrder() 호출 전 - 최종 상태 체크");
        for (int i = 0; i < turnOrder.Count; i++)
        {
            var p = turnOrder[i];
            if (p != null)
            {
                Debug.Log($"[BeginNewHand] 카드배분 전 최종상태 - {p.name}: canPlay={p.canPlay}, chips={p.playerChip}");
            }
        }

        // 3단계: canPlay 초기화 후에 카드 배분 순서 결정
        var order = BuildPreflopDealingOrder();

        Debug.Log($"[BeginNewHand] 카드 배분 순서: {order.Count}명");
        deck.PreflopDealInOrder(order);

        // 4단계: 게임 시작
        int utgSeatIdx = FirstToActPreflopSeatIndex();
        int utgTurnIdx = TurnIndexFromSeatIndex(utgSeatIdx);
        StartBettingRound(utgTurnIdx);

        Debug.Log($"[NewHand] BTN={buttonIndex + 1}, SB={sbIndex + 1}, BB={bbIndex + 1}, UTG={utgSeatIdx + 1}");
        Debug.Log("=== BeginNewHand END ===");
    }

    private List<Seat> BuildPreflopDealingOrder()
    {
        var order = new List<Seat>();

        Debug.Log($"[BuildPreflopDealingOrder] Building simple order - Total turnOrder: {turnOrder.Count}");

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
                        Debug.Log($"[BuildPreflopDealingOrder] ✅ Added {player.name} from seat #{seatIdx + 1}");
                        break;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[BuildPreflopDealingOrder] ❌ Skipped turnOrder[{i}]: player={player?.name}, chips={player?.playerChip}");
            }
        }

        Debug.Log($"[BuildPreflopDealingOrder] Final order: {order.Count} players");
        return order;
    }

    private void PostBlinds()
    {
        Debug.Log("=== PostBlinds START ===");

        // SB/BB 플레이어 찾기
        var sbPlayer = GetPlayerAtSeat(seatOrder[sbIndex]);
        var bbPlayer = GetPlayerAtSeat(seatOrder[bbIndex]);

        Debug.Log($"[PostBlinds] SB Player: {sbPlayer?.name}, canPlay={sbPlayer?.canPlay}");
        Debug.Log($"[PostBlinds] BB Player: {bbPlayer?.name}, canPlay={bbPlayer?.canPlay}");

        if (sbPlayer != null)
        {
            Debug.Log($"[PostBlinds] SB {sbPlayer.name} 블라인드 전: chips={sbPlayer.playerChip}, canPlay={sbPlayer.canPlay}");

            // 스몰 블라인드 처리
            // ... 여기서 canPlay 건드리는 코드 있는지 확인!

            Debug.Log($"[PostBlinds] SB {sbPlayer.name} 블라인드 후: chips={sbPlayer.playerChip}, canPlay={sbPlayer.canPlay}");
        }

        if (bbPlayer != null)
        {
            Debug.Log($"[PostBlinds] BB {bbPlayer.name} 블라인드 전: chips={bbPlayer.playerChip}, canPlay={bbPlayer.canPlay}");

            // 빅 블라인드 처리
            // ... 여기서 canPlay 건드리는 코드 있는지 확인!

            Debug.Log($"[PostBlinds] BB {bbPlayer.name} 블라인드 후: chips={bbPlayer.playerChip}, canPlay={bbPlayer.canPlay}");
        }

        Debug.Log("=== PostBlinds END ===");
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
        if (seatOrder.Count == 0) return -1;

        // 디버깅: 현재 버튼 정보 확인
        Debug.Log($"[FirstToActPostflopSeatIndex] buttonIndex: {buttonIndex}");
        Debug.Log($"[FirstToActPostflopSeatIndex] seatOrder.Count: {seatOrder.Count}");

        // 버튼 다음 좌석부터 시계방향으로 찾기
        int currentSeat = buttonIndex;

        // 최대 한 바퀴 돌면서 찾기
        for (int i = 0; i < seatOrder.Count; i++)
        {
            // 다음 좌석으로 이동
            currentSeat = (currentSeat + 1) % seatOrder.Count;

            Debug.Log($"[FirstToActPostflopSeatIndex] Checking seat #{currentSeat + 1} (index: {currentSeat})");

            // 해당 좌석에 플레이어가 있고, 게임에 참여 중인지 확인
            if (currentSeat >= 0 && currentSeat < seatOrder.Count)
            {
                var seat = seatOrder[currentSeat];
                var player = GetPlayerAtSeat(seat);

                Debug.Log($"[FirstToActPostflopSeatIndex] Seat #{currentSeat + 1}: Player = {(player != null ? player.name : "null")}, CanPlay = {(player != null ? player.canPlay.ToString() : "false")}");

                if (player != null && player.canPlay)
                {
                    Debug.Log($"[FirstToActPostflopSeatIndex] ✅ Found first actor at seat #{currentSeat + 1}: {player.name}");
                    return currentSeat;
                }
            }
        }

        Debug.LogWarning("[FirstToActPostflopSeatIndex] No valid first actor found, fallback to button");
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

        // 모든 플레이어 턴 종료
        foreach (var p in turnOrder)
            if (p != null) p.isMyTurn = false;

        currentIndex = Mathf.Clamp(firstTurnIndex, 0, turnOrder.Count - 1);

        // 여기가 핵심! 강제로 해당 플레이어만 턴 활성화
        if (turnOrder.Count > 0 && currentIndex >= 0 && currentIndex < turnOrder.Count)
        {
            var targetPlayer = turnOrder[currentIndex];
            if (targetPlayer != null)
            {
                targetPlayer.isMyTurn = true;

                Debug.Log($"[StartBettingRound] === FORCED TURN === ");
                Debug.Log($"[StartBettingRound] Street: {currentStreet}");
                Debug.Log($"[StartBettingRound] Target Index: {currentIndex}");
                Debug.Log($"[StartBettingRound] Target Player: {targetPlayer.name}");
                Debug.Log($"[StartBettingRound] Target isMyTurn: {targetPlayer.isMyTurn}");

                //다른 모든 플레이어 턴 확실히 비활성화
                for (int i = 0; i < turnOrder.Count; i++)
                {
                    if (i != currentIndex && turnOrder[i] != null)
                    {
                        turnOrder[i].isMyTurn = false;
                    }
                }
            }
        }

        lastAggressorIndex = -1;
        actorsToAct = ActivePlayersCount();

        Debug.Log($"[StartBettingRound] Final - CurrentIndex: {currentIndex}, First Player: {(turnOrder.Count > 0 && currentIndex < turnOrder.Count ? turnOrder[currentIndex].name : "-")}, Actors: {actorsToAct}");
    }
    public void StartBettingRoundBySeat(int firstSeatIndex)
    {
        if (firstSeatIndex < 0 || firstSeatIndex >= seatOrder.Count) return;

        var firstPlayer = GetPlayerAtSeat(seatOrder[firstSeatIndex]);
        if (firstPlayer == null)
        {
            Debug.LogError($"[StartBettingRoundBySeat] No player at seat #{firstSeatIndex + 1}");
            return;
        }

        // 모든 플레이어 턴 종료
        foreach (var p in turnOrder)
            if (p != null) p.isMyTurn = false;

        // 첫 번째 플레이어 턴 시작
        firstPlayer.isMyTurn = true;
        currentIndex = turnOrder.IndexOf(firstPlayer);

        Debug.Log($"[StartBettingRoundBySeat] Starting with {firstPlayer.name} at seat #{firstSeatIndex + 1}");

        lastAggressorIndex = -1;
        actorsToAct = ActivePlayersCount();
    }
    public int ActivePlayersCount()
    {
        int cnt = 0;
        foreach (var p in turnOrder)
            if (p != null && p.canPlay && p.playerChip > 0) cnt++;
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

            // 🎯 레이즈된 금액으로 업데이트
            beforeBettingChip = raisedAmount;
            Debug.Log($"[RegisterAction] {actor.name} raised to {raisedAmount} - beforeBettingChip updated");
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

        // 디버깅
        Debug.Log($"[NextTurnFrom] Current actor: {actor.name}, Current idx: {idx}");

        actor.isMyTurn = false;

        // 포스트플랍에서는 좌석 순서 강제 적용
        if (currentStreet != Street.Preflop)
        {
            // 현재 플레이어의 좌석 찾기
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
                // 다음 좌석부터 시계방향으로 찾기
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

                            Debug.Log($"[NextTurnFrom] PostFlop - Next: {nextPlayer.name} at seat #{nextSeatIdx + 1}");
                            return;
                        }
                    }
                }
            }
        }

        // 기존 로직 (프리플랍이나 좌석 기반 로직 실패 시)
        for (int step = 1; step <= turnOrder.Count; step++)
        {
            int next = (idx + step) % turnOrder.Count;
            var cand = turnOrder[next];
            if (cand != null && cand.canPlay && cand.playerChip > 0)
            {
                currentIndex = next;
                cand.isMyTurn = true;

                Debug.Log($"[NextTurnFrom] Fallback - Next: {cand.name}");
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

                // 모든 플레이어 턴 강제 종료
                foreach (var p in turnOrder)
                    if (p != null) p.isMyTurn = false;

                int flopFirstIdx = TurnIndexFromSeatIndex(FirstToActPostflopSeatIndex());
                StartBettingRound(flopFirstIdx);

                // 추가 보호: 다시 한 번 강제 설정
                if (flopFirstIdx >= 0 && flopFirstIdx < turnOrder.Count)
                {
                    var targetPlayer = turnOrder[flopFirstIdx];
                    if (targetPlayer != null)
                    {
                        // 모든 플레이어 턴 끄기
                        foreach (var p in turnOrder)
                            if (p != null) p.isMyTurn = false;

                        // 타겟 플레이어만 턴 켜기
                        targetPlayer.isMyTurn = true;
                        currentIndex = flopFirstIdx;

                        Debug.Log($"[AdvanceStreet-Flop] FORCED Turn to {targetPlayer.name} (idx: {flopFirstIdx})");
                    }
                }
                break;

            case Street.Flop:
                currentStreet = Street.Turn;
                deck.Turn();

                // 모든 플레이어 턴 강제 종료
                foreach (var p in turnOrder)
                    if (p != null) p.isMyTurn = false;

                int turnFirstIdx = TurnIndexFromSeatIndex(FirstToActPostflopSeatIndex());
                StartBettingRound(turnFirstIdx);

                // 추가 보호: 다시 한 번 강제 설정
                if (turnFirstIdx >= 0 && turnFirstIdx < turnOrder.Count)
                {
                    var targetPlayer = turnOrder[turnFirstIdx];
                    if (targetPlayer != null)
                    {
                        // 모든 플레이어 턴 끄기
                        foreach (var p in turnOrder)
                            if (p != null) p.isMyTurn = false;

                        // 타겟 플레이어만 턴 켜기
                        targetPlayer.isMyTurn = true;
                        currentIndex = turnFirstIdx;

                        Debug.Log($"[AdvanceStreet-Turn] FORCED Turn to {targetPlayer.name} (idx: {turnFirstIdx})");
                    }
                }
                break;

            case Street.Turn:
                currentStreet = Street.River;
                deck.River();

                // 모든 플레이어 턴 강제 종료
                foreach (var p in turnOrder)
                    if (p != null) p.isMyTurn = false;

                int riverFirstIdx = TurnIndexFromSeatIndex(FirstToActPostflopSeatIndex());
                StartBettingRound(riverFirstIdx);

                // 추가 보호: 다시 한 번 강제 설정
                if (riverFirstIdx >= 0 && riverFirstIdx < turnOrder.Count)
                {
                    var targetPlayer = turnOrder[riverFirstIdx];
                    if (targetPlayer != null)
                    {
                        // 모든 플레이어 턴 끄기
                        foreach (var p in turnOrder)
                            if (p != null) p.isMyTurn = false;

                        // 타겟 플레이어만 턴 켜기
                        targetPlayer.isMyTurn = true;
                        currentIndex = riverFirstIdx;

                        Debug.Log($"[AdvanceStreet-River] FORCED Turn to {targetPlayer.name} (idx: {riverFirstIdx})");
                    }
                }
                break;

            case Street.River:
                ResolveShowdown();
                break;

            default:
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

        // 다음 핸드: 좌석 재스캔 → 버튼 플레이어 기반 이동 → 이동 애니메이션
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
