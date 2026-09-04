using UnityEditor;
using UnityEngine;

public static class SlowAuraRadiusGizmo
{
    private static readonly Color RadiusColor = new(0.2f, 0.85f, 1f, 0.95f);

    [DrawGizmo(GizmoType.Selected)]
    private static void DrawRadius(TowerAttack towerAttack, GizmoType gizmoType)
    {
        SlowAuraUpgrade slowAura = FindSlowAuraUpgrade();
        if (slowAura == null || slowAura.effectRadius <= 0f)
            return;

        Vector3 center = towerAttack.transform.position;
        float radius = slowAura.effectRadius;

        Handles.color = RadiusColor;
        Handles.DrawWireDisc(center, Vector3.up, radius, 2f);
        Handles.DrawLine(center - Vector3.right * radius, center + Vector3.right * radius, 2f);
        Handles.DrawLine(center - Vector3.forward * radius, center + Vector3.forward * radius, 2f);
        Handles.Label(
            center + Vector3.right * radius,
            $"Slow Aura radius: {radius:0.##}");
    }

    private static SlowAuraUpgrade FindSlowAuraUpgrade()
    {
        BossRewardProvider provider = Object.FindFirstObjectByType<BossRewardProvider>(
            FindObjectsInactive.Include);
        if (provider == null || provider.rewards == null)
            return null;

        foreach (UpgradeBaseSO reward in provider.rewards)
        {
            if (reward is SlowAuraUpgrade slowAura)
                return slowAura;
        }

        return null;
    }
}
