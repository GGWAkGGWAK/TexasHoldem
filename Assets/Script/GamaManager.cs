using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GamaManager : MonoBehaviour
{
    public int smallBlind;       //스몰블라인드
    public int BigBlind;         //빅블라인드
    public float duration;       //듀레이션
    public float bettingTime;    //베팅 제한 시간
    public int pots;            //팟
    public int beforeBettingChip;       //직전 베팅칩
    public int beforeRaiseChip;         //베팅칩과 레이즈 칩의 차액

    public Text potsText;           //팟 텍스트

    public List<Player> turnOrder = new List<Player>();
    public int currentIndex = -1;

    private void Awake()
    {
        Transform canvas = GameObject.Find("Canvas").transform;
        potsText = canvas.Find("팟").GetComponent<Text>();        
    }
    void Start()
    {
        smallBlind = 10000;
        BigBlind = 20000;
        duration = 180;
        beforeBettingChip = 20000;
        BuildTurnOrderBySeats();
        SetFirstPlayer(0); // 첫 턴: 0번 플레이어
    }
    void Update()
    {
        potsText.text = "Pots: " + pots.ToString();
    }
    public void BuildTurnOrderBySeats()
    {
        turnOrder.Clear();

        GameObject[] seatObjs = GameObject.FindGameObjectsWithTag("Seat");
        if (seatObjs == null || seatObjs.Length == 0) return;

        // Seat 컴포넌트 수집
        var seats = new List<Seat>();
        foreach (var go in seatObjs)
        {
            var s = go.GetComponent<Seat>();
            if (s != null) seats.Add(s);
        }

        // ★ 중요: 좌석을 "계층(형제) 순서"로 정렬 (모두 같은 부모 밑에 있을 때 안정적)
        seats.Sort((a, b) =>
            a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex())
        );

        // (대안) 이름 숫자 기준 정렬을 쓰고 싶다면:
        // seats.Sort((a,b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        // 좌석 순서대로 Player를 찾고, 착석/참여 가능 조건으로 필터
        foreach (var seat in seats)
        {
            Player p = seat.GetComponentInChildren<Player>(true); // 비활성 자식도 탐색하려면 true
            if (p != null && seat.isSeated && p.canPlay)
            {
                turnOrder.Add(p);
            }
        }

        // 모두 턴 false로 초기화
        foreach (var p in turnOrder) p.isMyTurn = false;

        // 안전 로그
        Debug.Log($"[TurnOrder] 좌석 기준 플레이어 수: {turnOrder.Count}");
    }

    public void SetFirstPlayer(int index)
    {
        if (turnOrder.Count == 0)
        {
            currentIndex = -1;
            return;
        }

        foreach (var p in turnOrder) p.isMyTurn = false;

        currentIndex = Mathf.Clamp(index, 0, turnOrder.Count - 1);
        turnOrder[currentIndex].isMyTurn = true;

        Debug.Log($"첫 턴: {turnOrder[currentIndex].name}");
    }

    // --- 액터 이후의 다음 유효 플레이어에게 턴 넘기기 ---
    public void NextTurnFrom(Player actor)
    {
        if (turnOrder.Count == 0) return;

        int idx = turnOrder.IndexOf(actor);
        if (idx < 0) idx = currentIndex;

        actor.isMyTurn = false;

        // 다음 유효 후보 탐색: canPlay=true, 칩>0
        for (int step = 1; step <= turnOrder.Count; step++)
        {
            int next = (idx + step) % turnOrder.Count;
            var cand = turnOrder[next];

            if (cand != null && cand.canPlay && cand.playerChip > 0)
            {
                currentIndex = next;
                cand.isMyTurn = true;
                Debug.Log($"다음 턴: {cand.name}");
                return;
            }
        }

        // 유효한 다음 주자가 없다면: 라운드 종료 처리 포인트
        Debug.Log("유효한 다음 플레이어 없음 → 스트리트/라운드 종료 처리 필요");
    }
}
