using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public static class WinnerEvaluator
{
    // 공동 우승자(스플릿 포함) 반환
    public static List<Player> DecideWinners(List<Player> players, List<CardData> board5)
    {
        Debug.Log($"[WinnerEvaluator] DecideWinners 시작: {players.Count}명 입력");

        List<Player> winners = new List<Player>();
        HandValue bestValue = new HandValue(HandCategory.HighCard, new int[] { 0 });

        foreach (var p in players)
        {
            Debug.Log($"[WinnerEvaluator] 플레이어 {p.name}: canPlay={p.canPlay}, chips={p.playerChip}");

            // 🔥 핵심 수정: playerChip <= 0 조건 제거!
            // 올인한 플레이어도 승자가 될 수 있어야 함
            if (!p.canPlay)
            {
                Debug.Log($"[WinnerEvaluator] {p.name} canPlay=false로 제외");
                continue;
            }

            var holeCards = p.GetComponentsInChildren<Card>()
                             .Select(c => c.cardData)
                             .ToList();

            Debug.Log($"[WinnerEvaluator] {p.name} 홀 카드 수: {holeCards.Count}");

            if (holeCards.Count < 2)
            {
                Debug.Log($"[WinnerEvaluator] {p.name} 홀 카드 부족으로 제외");
                continue;
            }

            // 홀 카드 출력
            if (holeCards.Count >= 2)
            {
                Debug.Log($"[WinnerEvaluator] {p.name} 홀 카드: {holeCards[0].cardNumber} of {holeCards[0].cardShape}, {holeCards[1].cardNumber} of {holeCards[1].cardShape}");
            }

            HandValue hv = HandEvaluator.EvaluateBestFromHoleAndBoard(holeCards, board5);
            Debug.Log($"[WinnerEvaluator] {p.name} 핸드: {hv}");

            int cmp = HandEvaluator.CompareHands(hv, bestValue);
            Debug.Log($"[WinnerEvaluator] {p.name} vs 최고핸드 비교: {cmp}");

            if (cmp > 0)
            {
                Debug.Log($"[WinnerEvaluator] {p.name} 새로운 최고핸드! 이전 승자들 제거");
                winners.Clear();
                winners.Add(p);
                bestValue = hv;
            }
            else if (cmp == 0)
            {
                Debug.Log($"[WinnerEvaluator] {p.name} 동점! 승자에 추가");
                winners.Add(p);
            }
            else
            {
                Debug.Log($"[WinnerEvaluator] {p.name} 패배");
            }
        }

        Debug.Log($"[WinnerEvaluator] 최종 승자 {winners.Count}명:");
        foreach (var winner in winners)
        {
            Debug.Log($"  - 승자: {winner.name}");
        }

        return winners;
    }

    // 팟 나누기 (스플릿 시 균등 분배)
    public static void DistributePot(int totalPot, List<Player> winners)
    {
        if (winners.Count == 0) return;

        int share = totalPot / winners.Count;
        foreach (var w in winners)
        {
            w.playerChip += share;
        }

    }
}
