using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Pot
{
    public int amount;                    // 이 팟의 총 금액
    public HashSet<Player> eligible;      // 이 팟을 딸 수 있는 플레이어(폴드 제외)
}

public static class SidePot
{
    /// <summary>
    /// 모든 플레이어의 최종 기여액을 기반으로 사이드팟을 생성한다.
    /// - 금액 합산에는 폴드 플레이어도 포함 (이미 낸 돈은 팟에 남음)
    /// - 당첨 자격(eligible)은 canPlay==true 인 플레이어만
    /// </summary>
    public static List<Pot> BuildPots(List<Player> allPlayers)
    {
        // 기여액 맵
        var contrib = new Dictionary<Player, int>();
        foreach (var p in allPlayers)
        {
            if (p == null) continue;
            int c = Mathf.Max(0, p.contributedThisHand);
            if (c > 0) contrib[p] = c;
        }
        if (contrib.Count == 0) return new List<Pot>();

        // 레벨(기여액 구간) = 오름차순의 distinct 기여액
        var levels = contrib.Values.Distinct().OrderBy(v => v).ToList();

        var pots = new List<Pot>();
        int prev = 0;

        for (int i = 0; i < levels.Count; i++)
        {
            int level = levels[i];
            int delta = level - prev;
            if (delta <= 0) { prev = level; continue; }

            // 이 구간에 참여(돈이 걸린)한 모든 플레이어 수: contrib >= level
            var participantsForMoney = contrib.Where(kv => kv.Value >= level).Select(kv => kv.Key).ToList();
            int countMoney = participantsForMoney.Count;
            int potAmount = delta * countMoney;

            // 이 팟을 딸 자격(폴드 제외): canPlay==true AND contrib >= level
            var eligible = new HashSet<Player>(
                participantsForMoney.Where(p => p.canPlay)
            );

            pots.Add(new Pot { amount = potAmount, eligible = eligible });

            prev = level;
        }

        return pots;
    }

    /// <summary>
    /// 각 팟별로 승자를 뽑아 분배한다. (스플릿 시 균등 분배, 나머지는 첫 승자에게)
    /// </summary>
    public static string DistributeAllPots(List<Pot> pots, List<CardData> board5)
    {
        int totalPaid = 0;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < pots.Count; i++)
        {
            var pot = pots[i];
            if (pot.amount <= 0) continue;

            var eligibles = pot.eligible?.Where(p => p != null && p.canPlay).ToList() ?? new List<Player>();
            if (eligibles.Count == 0)
            {
                sb.AppendLine($"[Pot {i}] No eligible players (amount={pot.amount:N0})");
                Debug.LogWarning($"[SidePot] pot#{i} eligible=0 → 하우스에 귀속. amount={pot.amount}");
                continue;
            }

            // 팟별 승자 판정
            var winners = WinnerEvaluator.DecideWinners(eligibles, board5);
            if (winners.Count == 0)
            {
                sb.AppendLine($"[Pot {i}] No winners (amount={pot.amount:N0})");
                Debug.LogWarning($"[SidePot] pot#{i} winners=0 → 규칙상 처리 필요. amount={pot.amount}");
                continue;
            }

            int share = pot.amount / winners.Count;
            int rem = pot.amount % winners.Count;

            foreach (var w in winners)
            {
                w.playerChip += share;
            }

            // 남는 1~(n-1)칩은 첫 승자에게
            winners[0].playerChip += rem;

            totalPaid += pot.amount;

            // 문자열 요약
            sb.AppendLine($"[Pot {i}] {string.Join(", ", winners.Select(w => w.name))} " +
                          $"won {share + (rem > 0 ? rem : 0):N0} each ({pot.amount:N0} total)");
        }

        sb.AppendLine($"Total paid out: {totalPaid:N0}");

        // 문자열 반환
        return sb.ToString();
    }

}
