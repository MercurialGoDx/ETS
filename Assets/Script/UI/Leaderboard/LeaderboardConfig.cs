using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Настройки Steam-лидерборда в одном месте, применяются на старте сцены. Вешается на всегда-активный
/// объект (например, SteamManager) — важно, чтобы конфигурация задавалась ДО первого обращения к
/// таблице: иначе, если игрок умрёт раньше, чем откроет таблицу, отправка результата ушла бы с
/// дефолтными параметрами (окно таблицы стартует выключенным, поэтому само сконфигурировать не успеет).
/// </summary>
public class LeaderboardConfig : MonoBehaviour
{
    [Tooltip("Имя лидерборда в Steamworks. Должно точно совпадать с именем на partner-сайте.")]
    [SerializeField] private string leaderboardName = "SurvivalTime_v2";

    [Tooltip("ВКЛ — FindOrCreateLeaderboard (создаёт лидерборд, если его нет; удобно в разработке).\n" +
             "ВЫКЛ — FindLeaderboard (только ищет существующий; для релиза, чтобы клиент не плодил лидерборды).")]
    [SerializeField] private bool createIfMissing = true;

    [Tooltip("SteamID64 читеров/нарушителей — эти записи просто не показываются в таблице лидеров " +
             "(сама запись у Steam остаётся, удалить чужую запись через API нельзя). SteamID64 можно " +
             "увидеть в логе игры (Player.log) — при каждой загрузке таблицы в лог пишется ник + SteamID64.")]
    [SerializeField] private List<string> blockedSteamIds = new List<string>();

    private void Awake()
    {
        Leaderboards.Configure(leaderboardName, createIfMissing, blockedSteamIds);
    }
}
