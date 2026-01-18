using System.Collections.Generic;
using UnityEngine;

public class BossRewardProvider : MonoBehaviour
{
    [Tooltip("Сюда руками добавляешь все BossRewardDefinition, которые могут выпадать.")]
    public List<UpgradeBaseSO> rewards = new();

    public List<UpgradeBaseSO> PickThreeUnique()
    {
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
            var pick = PickOneWeighted(candidates);
            if (pick == null) break;
            result.Add(pick);
            candidates.Remove(pick);
        }

        return result;
    }

    private UpgradeBaseSO PickOneWeighted(List<UpgradeBaseSO> list)
    {
        if (list == null || list.Count == 0) return null;

        int total = 0;
        foreach (var r in list) total += Mathf.Max(1, r.weight);

        int roll = Random.Range(0, total);
        int acc = 0;

        foreach (var r in list)
        {
            acc += Mathf.Max(1, r.weight);
            if (roll < acc) return r;
        }

        return list[list.Count - 1];
    }
}
