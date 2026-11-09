using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum Street { Preflop, Flop, Turn, River, Showdown }
public enum ActionType { Fold, Check, Call, Bet, Raise, AllIn }

public class GameManager : MonoBehaviour
{
    private enum HandState { Waiting, Playing, BetweenHands }

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
    private readonly Dictionary<Player, int> actedAtBet = new Dictionary<Player, int>();
    private HandState handState;
    public float nextHandDelay = 3f;

    private DealerButton dealerButton;
    public bool CanStartHand => handState == HandState.Waiting;

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
        if (!CanStartHand) yield break;

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
    public void AddNewAIPlayerToEmptySeat(AIPersonality personality = AIPersonality.Normal)
    {
        Seat emptySeat = FindEmptySeat();

        if (emptySeat == null)
        {
            Debug.Log("💺 빈 좌석이 없습니다!");
            return;
        }

        GameObject newPlayerObj = CreateNewPlayer(emptySeat);

        if (newPlayerObj != null)
        {
            Player newPlayer = newPlayerObj.GetComponent<Player>();
            AIPlayer aiComponent = newPlayerObj.AddComponent<AIPlayer>();
            aiComponent.personality = personality;

            Debug.Log($"🤖 AI 플레이어가 {emptySeat.name}에 추가되었습니다!");
            Debug.Log($"   - 성격: {personality}");
            Debug.Log($"   - 초기 칩: {newPlayer.playerChip:N0}");
        }
    }

    public void AddMultipleAIPlayers(int count, AIPersonality personality = AIPersonality.Normal)
    {
        for (int i = 0; i < count; i++)
        {
            Seat emptySeat = FindEmptySeat();
            if (emptySeat == null)
            {
                Debug.LogWarning($"💺 {i}명의 AI 플레이어만 추가되었습니다. (빈 좌석 부족)");
                break;
            }

            GameObject newPlayerObj = CreateNewPlayer(emptySeat);
            if (newPlayerObj != null)
            {
                AIPlayer aiComponent = newPlayerObj.AddComponent<AIPlayer>();
                aiComponent.personality = personality;
                emptySeat.isSeated = true;
            }
        }

        Debug.Log($"🤖 {count}명의 AI 플레이어가 추가되었습니다!");
    }

    public void AddMixedAIPlayers()
    {
        AIPersonality[] personalities = new AIPersonality[]
        {
            AIPersonality.Tight,
            AIPersonality.Normal,
            AIPersonality.Aggressive,
            AIPersonality.Loose
        };

        foreach (var personality in personalities)
        {
            Seat emptySeat = FindEmptySeat();
            if (emptySeat == null) break;

            GameObject newPlayerObj = CreateNewPlayer(emptySeat);
            if (newPlayerObj != null)
            {
                AIPlayer aiComponent = newPlayerObj.AddComponent<AIPlayer>();
                aiComponent.personality = personality;
                emptySeat.isSeated = true;
            }
        }

        Debug.Log("🤖 다양한 성격의 AI 플레이어들이 추가되었습니다!");
    }

    public void AddAIPlayerToSpecificSeat(int seatIndex, AIPersonality personality = AIPersonality.Normal)
    {
        if (seatIndex < 0 || seatIndex >= seatOrder.Count)
        {
            Debug.LogError($"❌ 잘못된 좌석 인덱스: {seatIndex}");
            return;
        }

        Seat targetSeat = seatOrder[seatIndex];
        if (targetSeat == null)
        {
            Debug.LogError($"❌ 좌석 {seatIndex}을 찾을 수 없습니다!");
            return;
        }

        if (GetPlayerAtSeat(targetSeat) != null)
        {
            Debug.LogWarning($"⚠️ 좌석 {seatIndex}에는 이미 플레이어가 있습니다!");
            return;
        }

        GameObject newPlayerObj = CreateNewPlayer(targetSeat);
        if (newPlayerObj != null)
        {
            AIPlayer aiComponent = newPlayerObj.AddComponent<AIPlayer>();
            aiComponent.personality = personality;
            targetSeat.isSeated = true;

            Debug.Log($"🤖 AI 플레이어가 좌석 {seatIndex}에 추가되었습니다! (성격: {personality})");
        }
    }

    public void ChangeAIPersonality(Player targetPlayer, AIPersonality newPersonality)
    {
        AIPlayer aiComponent = targetPlayer.GetComponent<AIPlayer>();
        if (aiComponent == null)
        {
            Debug.LogError($"❌ {targetPlayer.name}은 AI 플레이어가 아닙니다!");
            return;
        }

        aiComponent.personality = newPersonality;
        Debug.Log($"🎭 {targetPlayer.name}의 성격이 {newPersonality}로 변경되었습니다!");
    }

    public void SetAIActionDelay(Player targetPlayer, float delay)
    {
        AIPlayer aiComponent = targetPlayer.GetComponent<AIPlayer>();
        if (aiComponent == null)
        {
            Debug.LogError($"❌ {targetPlayer.name}은 AI 플레이어가 아닙니다!");
            return;
        }

        aiComponent.actionDelay = delay;
        Debug.Log($"⏱️ {targetPlayer.name}의 액션 속도가 {delay}초로 변경되었습니다!");
    }

    public void PrintAllAIPlayers()
    {
        Debug.Log("═══════════════════════════════════════");
        Debug.Log("         현재 게임의 AI 플레이어 목록");
        Debug.Log("═══════════════════════════════════════");

        int aiCount = 0;
        foreach (var player in turnOrder)
        {
            AIPlayer ai = player.GetComponent<AIPlayer>();
            if (ai != null)
            {
                aiCount++;
                Debug.Log($"🤖 [{aiCount}] {player.name}");
                Debug.Log($"    - 성격: {ai.personality}");
                Debug.Log($"    - 칩: {player.playerChip:N0}");
                Debug.Log($"    - 액션 속도: {ai.actionDelay}초");
            }
        }

        if (aiCount == 0)
        {
            Debug.Log("❌ AI 플레이어가 없습니다!");
        }
        else
        {
            Debug.Log("═══════════════════════════════════════");
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
        for (int offset = 1; offset <= seatOrder.Count; offset++)
        {
            int next = (buttonIndex + offset) % seatOrder.Count;
            var player = GetPlayerAtSeat(seatOrder[next]);
            if (player != null && player.playerChip > 0)
            {
                buttonIndex = next;
                return;
            }
        }
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

        var player = (buttonIndex >= 0 && buttonIndex < seatOrder.Count) ? GetPlayerAtSeat(seatOrder[buttonIndex]) : null;
        if (player != null && player.gameObject != null)
        {
            dealerButton.TeleportTo(player.transform);
        }
    }
    private void MoveDealerButton()
    {
        if (dealerButton == null) return;

        var player = (buttonIndex >= 0 && buttonIndex < seatOrder.Count) ? GetPlayerAtSeat(seatOrder[buttonIndex]) : null;
        if (player != null && player.gameObject != null)
        {
            dealerButton.MoveTo(player.transform);
        }
    }
    // ========== 새 핸드 ==========
    public void BeginNewHand()
    {
        if (!CanStartHand) return;

        var deck = FindObjectOfType<Deck>();
        if (deck == null) return;

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
            p.contributedThisRound = 0;

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
                ShowWinnersUI(new List<Player> { winner },
                    suffix: $"\n🎉 TOURNAMENT CHAMPION! 🎉\n총 상금: {winner.playerChip:N0}");
            }
            return;
        }

        var order = BuildPreflopDealingOrder();
        if (order.Count != playersWithChips || !deck.CanDealHand(order.Count))
        {
            Debug.LogError("[BeginNewHand] 좌석이나 덱이 준비되지 않았습니다.");
            return;
        }

        handState = HandState.Playing;
        HideWinnersUI();
        pots = 0;
        beforeBettingChip = 0;
        currentStreet = Street.Preflop;
        buttonIndex = Mathf.Clamp(buttonIndex, 0, seatOrder.Count - 1);
        if (playersWithChips == 2 && !IsActiveSeat(buttonIndex))
            buttonIndex = NextSeatWithPlayerFrom(buttonIndex);

        sbIndex = playersWithChips == 2 ? buttonIndex : NextSeatWithPlayerFrom(buttonIndex);
        bbIndex = NextSeatWithPlayerFrom(sbIndex);
        TeleportDealerButton();

        deck.PrepareForHand();
        PostBlinds();
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
            if (player != null && player.canPlay)
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
        PostBlind(GetPlayerAtSeat(seatOrder[sbIndex]), smallBlind);
        PostBlind(GetPlayerAtSeat(seatOrder[bbIndex]), bigBlind);
        beforeBettingChip = bigBlind;
    }

    private void PostBlind(Player player, int blind)
    {
        int posted = Mathf.Min(blind, player.playerChip);
        player.playerChip -= posted;
        player.contributedThisHand = posted;
        player.contributedThisRound = posted;
        player.isAllIn = player.playerChip == 0;
        pots += posted;
    }

    private bool IsActiveSeat(int index)
    {
        var player = GetPlayerAtSeat(seatOrder[index]);
        return player != null && player.canPlay && player.playerChip > 0;
    }

    public bool IsInPosition(Player player)
    {
        for (int offset = 0; offset < seatOrder.Count; offset++)
        {
            int index = (buttonIndex - offset + seatOrder.Count) % seatOrder.Count;
            if (IsActiveSeat(index))
                return GetPlayerAtSeat(seatOrder[index]) == player;
        }

        return false;
    }

    private int FirstToActPreflopSeatIndex() => NextSeatWithPlayerFrom(bbIndex);

    public int MinimumRaiseIncrement => Mathf.Max(bigBlind, beforeRaiseChip);
    public int MinimumRaiseTo => beforeBettingChip + MinimumRaiseIncrement;
    public Player BigBlindPlayer => bbIndex >= 0 && bbIndex < seatOrder.Count
        ? GetPlayerAtSeat(seatOrder[bbIndex])
        : null;

    public int GetCallCost(Player player)
    {
        int target = beforeBettingChip;
        if (PlayersWithChipsCount() == 1)
        {
            // A lone stack only needs to match the chips opponents actually posted.
            target = 0;
            foreach (var opponent in turnOrder)
                if (opponent != null && opponent != player && opponent.canPlay)
                    target = Mathf.Max(target, opponent.contributedThisRound);
        }

        return Mathf.Max(0, target - player.contributedThisRound);
    }

    public bool CanIncreaseBet(Player player)
    {
        return player.playerChip > GetCallCost(player)
            && PlayersWithChipsCount() > 1
            && (!actedAtBet.TryGetValue(player, out int lastBet)
                || beforeBettingChip - lastBet >= MinimumRaiseIncrement);
    }

    public void StartBettingRound(int firstTurnIndex)
    {
        if (currentStreet != Street.Preflop)
        {
            beforeBettingChip = 0;
            foreach (var player in turnOrder)
                if (player != null)
                    player.contributedThisRound = 0;
        }

        beforeRaiseChip = bigBlind;
        actedAtBet.Clear();
        ContinueBettingFrom(firstTurnIndex);
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

    public void RegisterAction(Player actor)
    {
        int increase = actor.contributedThisRound - beforeBettingChip;
        if (increase > 0)
        {
            if (increase >= MinimumRaiseIncrement)
                beforeRaiseChip = increase;
            beforeBettingChip = actor.contributedThisRound;
        }

        actedAtBet[actor] = beforeBettingChip;
        if (ActivePlayersCount() == 1)
        {
            WinByAllFold();
            return;
        }

        NextTurnFrom(actor);
    }

    public void NextTurnFrom(Player actor)
    {
        ContinueBettingFrom(turnOrder.IndexOf(actor) + 1);
    }

    private void ContinueBettingFrom(int firstIndex)
    {
        foreach (var player in turnOrder)
            if (player != null)
                player.isMyTurn = false;

        int playersWithChips = PlayersWithChipsCount();
        for (int offset = 0; offset < turnOrder.Count; offset++)
        {
            int index = (firstIndex + offset) % turnOrder.Count;
            var player = turnOrder[index];
            if (player == null || !player.canPlay || player.playerChip == 0)
                continue;

            bool owesChips = GetCallCost(player) > 0;
            if (owesChips || (playersWithChips > 1 && !actedAtBet.ContainsKey(player)))
            {
                currentIndex = index;
                player.isMyTurn = true;
                return;
            }
        }

        currentIndex = -1;
        AdvanceStreet();
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

        if (PlayersWithChipsCount() <= 1)
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
        int firstIdx = TurnIndexFromSeatIndex(NextSeatWithPlayerFrom(buttonIndex));
        StartBettingRound(firstIdx);
    }

    // ========== 쇼다운/올폴드 ==========
    private void ResolveShowdown()
    {
        if (handState != HandState.Playing) return;

        var deck = FindObjectOfType<Deck>();
        List<CardData> board5 = deck.GetBoardCardData();
        handState = HandState.BetweenHands;
        currentStreet = Street.Showdown;
        currentIndex = -1;

        if (board5.Count != 5 || turnOrder.Any(p => p != null && p.canPlay
            && p.GetComponentsInChildren<Card>().Length != 2))
        {
            foreach (var player in turnOrder)
                if (player != null)
                    player.playerChip += player.contributedThisHand;

            pots = 0;
            Debug.LogError("[GameManager] 카드가 부족해 핸드를 무효 처리하고 칩을 반환했습니다.");
            ShowWinnersUI(new List<Player>(), "\nHand voided. Chips returned.");
            StartCoroutine(Co_NextHandAfterDelay(nextHandDelay));
            return;
        }

        var allPots = SidePot.BuildPots(turnOrder);
        string potsSummary = SidePot.DistributeAllPots(allPots, board5, out var winners);
        pots = 0;
        ShowWinnersUI(winners, "\n" + potsSummary);

        // 🆕 아웃된 플레이어 처리 추가
        ProcessEliminatedPlayers();

        StartCoroutine(Co_NextHandAfterDelay(nextHandDelay));
    }

    private void WinByAllFold()
    {
        if (handState != HandState.Playing) return;

        handState = HandState.BetweenHands;
        currentIndex = -1;
        var alive = new List<Player>();
        foreach (var p in turnOrder)
            if (p != null && p.canPlay) alive.Add(p);

        if (alive.Count == 1)
        {
            alive[0].playerChip += pots;
            this.pots = 0;
            ShowWinnersUI(new List<Player> { alive[0] }, " (All Fold)");
        }
        else
        {
            ShowWinnersUI(new List<Player>(), "");
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
        handState = HandState.Waiting;
        BeginNewHand();
    }

    // ========== WinnerText ==========
    private void ShowWinnersUI(List<Player> winners, string suffix)
    {
        if (winnerText == null) return;

        string names = string.Join(", ", winners.ConvertAll(w => w.name));
        winnerText.text = (winners.Count == 0 ? "No Winner" : $"🏆 {names}") + suffix;
        winnerText.gameObject.SetActive(true);
    }

    private void HideWinnersUI()
    {
        if (winnerText != null)
            winnerText.gameObject.SetActive(false);
    }
}
