using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum HandCategory
{
    HighCard = 0,
    OnePair = 1,
    TwoPair = 2,
    ThreeOfAKind = 3,
    Straight = 4,
    Flush = 5,
    FullHouse = 6,
    FourOfAKind = 7,
    StraightFlush = 8
}

public struct HandValue
{
    public HandCategory Category;
    public List<int> Tiebreakers;   // [핸드 랭크, 킥커1, 킥커2…]

    public HandValue(HandCategory cat, IEnumerable<int> tie)
    {
        Category = cat;
        Tiebreakers = new List<int>(tie);
    }

    public override string ToString()
    {
        return $"{Category} [{string.Join(",", Tiebreakers)}]";
    }
}

public static class HandEvaluator
{
    public static HandValue EvaluateBestFromHoleAndBoard(List<CardData> hole2, List<CardData> board5)
    {
        var seven = new List<CardData>(hole2.Count + board5.Count);
        seven.AddRange(hole2);
        seven.AddRange(board5);
        return Evaluate7(seven);
    }

    public static HandValue Evaluate7(List<CardData> cards7)
    {
        if (cards7 == null || cards7.Count != 7)
        {
            Debug.LogError("Evaluate7: cards must be 7.");
            return new HandValue(HandCategory.HighCard, new int[] { 0 });
        }

        var ranks = cards7.Select(c => c.cardNumber).ToList();
        var suits = cards7.Select(c => c.cardShape).ToList();

        // 슈트 그룹
        var suitGroups = new Dictionary<int, List<int>>();
        for (int i = 0; i < 7; i++)
        {
            if (!suitGroups.ContainsKey(suits[i])) suitGroups[suits[i]] = new List<int>();
            suitGroups[suits[i]].Add(ranks[i]);
        }

        // 스트레이트 플러시
        foreach (var kv in suitGroups)
        {
            if (kv.Value.Count >= 5)
            {
                var flushRanks = kv.Value.Distinct().ToList();
                flushRanks.Sort();
                if (flushRanks.Contains(14)) flushRanks.Add(1);
                int sfHigh = FindStraightHigh(flushRanks);
                if (sfHigh > 0)
                    return new HandValue(HandCategory.StraightFlush, new int[] { sfHigh });
            }
        }

        // 랭크별 카운트
        var countByRank = new Dictionary<int, int>();
        foreach (int r in ranks)
            countByRank[r] = countByRank.TryGetValue(r, out var n) ? n + 1 : 1;

        var allRanksDesc = countByRank.Keys.ToList();
        allRanksDesc.Sort((a, b) => b.CompareTo(a));

        // 포카드
        int fourKindRank = allRanksDesc.FirstOrDefault(r => countByRank[r] == 4);
        if (fourKindRank != 0)
        {
            int kicker = allRanksDesc.First(r => r != fourKindRank);
            return new HandValue(HandCategory.FourOfAKind, new int[] { fourKindRank, kicker });
        }

        // 풀하우스
        var trips = allRanksDesc.Where(r => countByRank[r] == 3).ToList();
        var pairs = allRanksDesc.Where(r => countByRank[r] == 2).ToList();

        if (trips.Count >= 2)
            return new HandValue(HandCategory.FullHouse, new int[] { trips[0], trips[1] });
        if (trips.Count >= 1 && pairs.Count >= 1)
            return new HandValue(HandCategory.FullHouse, new int[] { trips[0], pairs[0] });

        // 플러시
        foreach (var kv in suitGroups)
        {
            if (kv.Value.Count >= 5)
            {
                var top5 = kv.Value.OrderByDescending(x => x).Take(5).ToList();
                return new HandValue(HandCategory.Flush, top5);
            }
        }

        // 스트레이트
        var uniques = ranks.Distinct().ToList();
        uniques.Sort();
        if (uniques.Contains(14)) uniques.Add(1);
        int stHigh = FindStraightHigh(uniques);
        if (stHigh > 0)
            return new HandValue(HandCategory.Straight, new int[] { stHigh });

        // 트리플
        if (trips.Count >= 1)
        {
            int t = trips[0];
            var kickers = allRanksDesc.Where(r => r != t).Take(2).ToList();
            return new HandValue(HandCategory.ThreeOfAKind, new List<int> { t }.Concat(kickers));
        }

        // 투페어
        if (pairs.Count >= 2)
        {
            int p1 = pairs[0];
            int p2 = pairs[1];
            int kicker = allRanksDesc.First(r => r != p1 && r != p2);
            return new HandValue(HandCategory.TwoPair, new int[] { p1, p2, kicker });
        }

        // 원페어
        if (pairs.Count >= 1)
        {
            int p = pairs[0];
            var kickers = allRanksDesc.Where(r => r != p).Take(3).ToList();
            return new HandValue(HandCategory.OnePair, new List<int> { p }.Concat(kickers));
        }

        // 하이카드
        var topFive = allRanksDesc.Take(5).ToList();
        return new HandValue(HandCategory.HighCard, topFive);
    }

    private static int FindStraightHigh(List<int> sortedAsc)
    {
        if (sortedAsc.Count < 5) return 0;
        int run = 1, bestHigh = 0;
        for (int i = 1; i < sortedAsc.Count; i++)
        {
            if (sortedAsc[i] == sortedAsc[i - 1] + 1) run++;
            else if (sortedAsc[i] != sortedAsc[i - 1]) run = 1;
            if (run >= 5) bestHigh = sortedAsc[i];
        }
        return bestHigh;
    }

    public static int CompareHands(HandValue a, HandValue b)
    {
        if (a.Category != b.Category)
            return a.Category.CompareTo(b.Category);

        int n = Mathf.Max(a.Tiebreakers.Count, b.Tiebreakers.Count);
        for (int i = 0; i < n; i++)
        {
            int av = (i < a.Tiebreakers.Count) ? a.Tiebreakers[i] : 0;
            int bv = (i < b.Tiebreakers.Count) ? b.Tiebreakers[i] : 0;
            if (av != bv) return av.CompareTo(bv);
        }
        return 0;
    }
}
