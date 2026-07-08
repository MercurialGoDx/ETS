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
    [SerializeField] private string leaderboardName = "SurvivalTime";

    [Tooltip("ВКЛ — FindOrCreateLeaderboard (создаёт лидерборд, если его нет; удобно в разработке).\n" +
             "ВЫКЛ — FindLeaderboard (только ищет существующий; для релиза, чтобы клиент не плодил лидерборды).")]
    [SerializeField] private bool createIfMissing = true;

    private void Awake()
    {
        Leaderboards.Configure(leaderboardName, createIfMissing);
    }
}
