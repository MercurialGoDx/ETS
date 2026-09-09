using System;
using System.Collections.Generic;

/// <summary>
/// Лобби на двоих для дуэли. UI работает только через этот интерфейс, поэтому экран лобби
/// собирается и проверяется на заглушке (<see cref="StubLobbyService"/>) без запущенного
/// Steam, а потом реализация подменяется на настоящую без единой правки в UI — ровно так же,
/// как сделано у таблицы лидеров.
///
/// Обмена данными во время боя нет и не планируется: лобби нужно, чтобы раздать сид перед
/// забегом и собрать результаты после. Всё остальное игроки играют у себя.
/// </summary>
public interface ILobbyService
{
    bool IsInLobby { get; }

    /// <summary>Хост создаёт лобби и владеет его данными; гость только читает.</summary>
    bool IsHost { get; }

    /// <summary>Короткий код для передачи второму игроку. Пусто, если лобби нет.</summary>
    string Code { get; }

    IReadOnlyList<LobbyMember> Members { get; }

    void Create(Action<LobbyResult> onDone);

    void JoinByCode(string code, Action<LobbyResult> onDone);

    void Leave();

    /// <summary>Открывает оверлей Steam с выбором друга для приглашения.</summary>
    void InviteFriend();

    /// <summary>Данные лобби пишет только хост: сид, версия сборки, состояние матча.</summary>
    void SetLobbyValue(string key, string value);

    string GetLobbyValue(string key);

    /// <summary>Свои данные пишет каждый участник: готовность, результат забега.</summary>
    void SetMemberValue(string key, string value);

    string GetMemberValue(ulong memberId, string key);

    /// <summary>Кто-то вошёл или вышел.</summary>
    event Action MembersChanged;

    /// <summary>
    /// Участник пропал, и указано — сам ушёл или потерял связь. Отдельно от
    /// <see cref="MembersChanged"/>: там видно только, что состав изменился, а причину
    /// Steam сообщает один раз, в момент события.
    /// </summary>
    event Action<ulong, LobbyDeparture> MemberLeft;

    /// <summary>Изменились данные лобби или участника — например, хост выдал сид.</summary>
    event Action LobbyDataChanged;
}
