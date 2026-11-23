using System;
using System.Collections.Generic;
using ETS.Multiplayer;
using UnityEngine;

/// <summary>
/// Заглушка лобби для редактора без Steam. Позволяет собрать и проверить экран лобби,
/// не поднимая два клиента: создание и вход всегда успешны, второй участник появляется
/// сразу же — как будто друг уже принял приглашение.
///
/// Ничего никуда не отправляет: данные живут в памяти этого экземпляра.
/// </summary>
public class StubLobbyService : ILobbyService
{
    private const ulong LocalId = 1;
    private const ulong FakeGuestId = 2;

    public event Action MembersChanged;

    // Заглушка живёт в одном процессе: терять связь тут не с кем, событие есть только
    // ради контракта интерфейса.
#pragma warning disable 67
    public event Action<ulong, LobbyDeparture> MemberLeft;
#pragma warning restore 67
    public event Action LobbyDataChanged;

    private readonly List<LobbyMember> members = new List<LobbyMember>();
    private readonly Dictionary<string, string> lobbyValues = new Dictionary<string, string>();
    private readonly Dictionary<ulong, Dictionary<string, string>> memberValues =
        new Dictionary<ulong, Dictionary<string, string>>();

    private bool inLobby;
    private bool isHost;
    private string code = string.Empty;

    public bool IsInLobby => inLobby;
    public bool IsHost => isHost;
    public string Code => code;
    public IReadOnlyList<LobbyMember> Members => members;

    public void Create(Action<LobbyResult> onDone)
    {
        code = LobbyCode.Generate(bound => UnityEngine.Random.Range(0, bound));
        Enter(asHost: true);
        Debug.Log($"[StubLobby] Лобби создано, код {code} — заглушка, Steam не задействован.");
        onDone?.Invoke(LobbyResult.Ok);
    }

    public void JoinByCode(string rawCode, Action<LobbyResult> onDone)
    {
        string normalized = LobbyCode.Normalize(rawCode);
        if (normalized == null)
        {
            onDone?.Invoke(LobbyResult.InvalidCode);
            return;
        }

        code = normalized;
        Enter(asHost: false);
        Debug.Log($"[StubLobby] Вход по коду {code} — заглушка, вход всегда успешен.");
        onDone?.Invoke(LobbyResult.Ok);
    }

    private void Enter(bool asHost)
    {
        inLobby = true;
        isHost = asHost;

        members.Clear();
        members.Add(new LobbyMember(LocalId, "Вы", asHost));
        members.Add(new LobbyMember(FakeGuestId, "Соперник (заглушка)", !asHost));

        MembersChanged?.Invoke();
    }

    public void Leave()
    {
        inLobby = false;
        isHost = false;
        code = string.Empty;
        members.Clear();
        lobbyValues.Clear();
        memberValues.Clear();
        MembersChanged?.Invoke();
    }

    public void InviteFriend()
    {
        Debug.Log("[StubLobby] Оверлей приглашения недоступен без Steam.");
    }

    public void SetLobbyValue(string key, string value)
    {
        if (!inLobby || !isHost)
            return;

        lobbyValues[key] = value ?? string.Empty;
        LobbyDataChanged?.Invoke();
    }

    public string GetLobbyValue(string key)
    {
        return lobbyValues.TryGetValue(key, out string value) ? value : string.Empty;
    }

    public void SetMemberValue(string key, string value)
    {
        if (!inLobby)
            return;

        if (!memberValues.TryGetValue(LocalId, out var bag))
        {
            bag = new Dictionary<string, string>();
            memberValues[LocalId] = bag;
        }

        bag[key] = value ?? string.Empty;
        LobbyDataChanged?.Invoke();
    }

    public string GetMemberValue(ulong memberId, string key)
    {
        return memberValues.TryGetValue(memberId, out var bag) && bag.TryGetValue(key, out string value)
            ? value
            : string.Empty;
    }
}
