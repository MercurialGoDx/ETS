/// <summary>
/// Переводит оружие/улучшение в компактный числовой id и обратно, используя те же
/// списки, что и магазин (ShopManager.availableWeapons/availableUpgrades) как общий
/// каталог: id [0..оружия) — оружие, id [оружия..оружия+улучшения) — улучшения.
/// Стабилен в рамках одной версии игры — этого достаточно, т.к. лидерборд всё равно
/// переименовывается при патчах, меняющих баланс (см. LeaderboardConfig).
/// </summary>
public static class BuildCatalog
{
    public static int WeaponId(ShopManager shop, WeaponDefinition def)
    {
        if (shop == null || shop.availableWeapons == null || def == null)
            return -1;

        return shop.availableWeapons.IndexOf(def);
    }

    public static int UpgradeId(ShopManager shop, UpgradeBaseSO upgrade)
    {
        if (shop == null || shop.availableUpgrades == null || upgrade == null)
            return -1;

        int index = shop.availableUpgrades.IndexOf(upgrade);
        if (index < 0)
            return -1;

        int weaponCount = shop.availableWeapons != null ? shop.availableWeapons.Count : 0;
        return weaponCount + index;
    }

    /// <summary>Обратное преобразование: id → оружие ИЛИ улучшение (ровно один из двух будет не null).</summary>
    public static bool TryResolve(ShopManager shop, int id, out WeaponDefinition weapon, out UpgradeBaseSO upgrade)
    {
        weapon = null;
        upgrade = null;

        if (shop == null || id < 0)
            return false;

        int weaponCount = shop.availableWeapons != null ? shop.availableWeapons.Count : 0;

        if (id < weaponCount)
        {
            weapon = shop.availableWeapons[id];
            return weapon != null;
        }

        int upgradeIndex = id - weaponCount;
        if (shop.availableUpgrades != null && upgradeIndex >= 0 && upgradeIndex < shop.availableUpgrades.Count)
        {
            upgrade = shop.availableUpgrades[upgradeIndex];
            return upgrade != null;
        }

        return false;
    }
}
