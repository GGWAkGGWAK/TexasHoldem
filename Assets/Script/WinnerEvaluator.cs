using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public static class WinnerEvaluator
{
    // 공동 우승자(스플릿 포함) 반환
    public static List<Player> DecideWinners(List<Player> players, List<CardData> board5)
    {
        List<Player> winners = new List<Player>();
        HandValue bestValue = new HandValue(HandCategory.HighCard, new int[] { 0 });

        foreach (var p in players)
        {
            if (!p.canPlay || p.playerChip <= 0) continue;

            var holeCards = p.GetComponentsInChildren<Card>()
                             .Select(c => c.cardData)
                             .ToList();
            if (holeCards.Count < 2) continue;

            HandValue hv = HandEvaluator.EvaluateBestFromHoleAndBoard(holeCards, board5);
            int cmp = HandEvaluator.CompareHands(hv, bestValue);

            if (cmp > 0)
            {
                winners.Clear();
                winners.Add(p);
                bestValue = hv;
            }
            else if (cmp == 0)
            {
                winners.Add(p);
            }
        }

        Debug.Log($"🏆 승자 수: {winners.Count}, 족보: {bestValue.Category}");
        foreach (var w in winners)
            Debug.Log($"→ Winner: {w.name}");

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

        Debug.Log($"💰 {totalPot}칩을 {winners.Count}명에게 분배 ({share}씩)");
    }
}
