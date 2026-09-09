/// <summary>Участник лобби в виде, не зависящем от Steam — чтобы UI и заглушка не знали про CSteamID.</summary>
public readonly struct LobbyMember
{
    /// <summary>SteamID участника. Для заглушки — синтетический.</summary>
    public readonly ulong Id;

    public readonly string Name;

    public readonly bool IsHost;

    public LobbyMember(ulong id, string name, bool isHost)
    {
        Id = id;
        Name = name;
        IsHost = isHost;
    }
}

/// <summary>Чем закончилась попытка создать лобби или войти в него.</summary>
public enum LobbyResult
{
    Ok = 0,

    /// <summary>Steam не инициализирован — мультиплеера в этом запуске нет.</summary>
    NoSteam = 1,

    /// <summary>Код не проходит проверку формата, до Steam дело не дошло.</summary>
    InvalidCode = 2,

    /// <summary>Формат верный, но лобби с таким кодом не нашлось (или оно уже закрыто).</summary>
    NotFound = 3,

    /// <summary>Лобби нашлось, но войти не удалось: заполнено, закрыто или отказал Steam.</summary>
    Failed = 4,
}

/// <summary>
/// Почему участник пропал из лобби. Разница важна: сам вышел — ждать некого, матч идёт
/// дальше; оборвалась связь — держим паузу и место в лобби, пока он не вернётся.
/// </summary>
public enum LobbyDeparture
{
    /// <summary>Нажал «выйти», закрыл игру или был исключён — вернуться сам не собирается.</summary>
    Left = 0,

    /// <summary>Пропала связь со Steam: клиент упал или отвалилась сеть.</summary>
    Disconnected = 1,
}
