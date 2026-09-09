using System.Collections.Generic;
using ETS.Multiplayer;
using UnityEngine;

public class BossRewardProvider : MonoBehaviour
{
    [Tooltip("Сюда руками добавляешь все BossRewardDefinition, которые могут выпадать.")]
    public List<UpgradeBaseSO> rewards = new();

    /// <summary>
    /// Три награды за босса. В дуэли бросок адресуется НОМЕРОМ БОССА, а не счётчиком выданных
    /// наград: если один игрок не добил первого босса, его награда за второго обязана совпасть
    /// с чужой наградой за второго, а не за первого.
    /// </summary>
    public List<UpgradeBaseSO> PickThreeUnique(int bossOrdinal = 0, int selectionIndex = 0, int rerollIndex = 0)
    {
        // Реролл обязан входить в адрес розыгрыша: иначе в дуэли он вернул бы ту же тройку,
        // потому что номер босса и номер выбора при обновлении не меняются. Заряды реролла
        // у игроков свои, так что после обновления их награды законно расходятся.
        int drawRound = DuelRandom.Compose(DuelRandom.Compose(bossOrdinal, selectionIndex), rerollIndex);
        List<UpgradeBaseSO> candidates = new();
        foreach (var r in rewards)
        {
            if (r == null) continue;
            candidates.Add(r);
        }

        List<UpgradeBaseSO> result = new(3);

        // без повторов
        for (int i = 0; i < 3; i++)
        {
            var pick = PickOneWeighted(candidates, DuelRandom.Compose(drawRound, i));
            if (pick == null) break;
            result.Add(pick);
            candidates.Remove(pick);
        }

        return result;
    }

    private UpgradeBaseSO PickOneWeighted(List<UpgradeBaseSO> list, int drawIndex)
    {
        if (list == null || list.Count == 0) return null;

        int total = 0;
        foreach (var r in list) total += Mathf.Max(1, r.weight);

        int roll = DuelSession.IsSeeded
            ? DuelRandom.Range(DuelSession.Seed, DuelStream.BossReward, drawIndex, 0, total)
            : Random.Range(0, total);
        int acc = 0;

        foreach (var r in list)
        {
            acc += Mathf.Max(1, r.weight);
            if (roll < acc) return r;
        }

        return list[list.Count - 1];
    }
}
