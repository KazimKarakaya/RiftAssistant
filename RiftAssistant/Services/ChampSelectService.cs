using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RiftAssistant.Core;
using RiftAssistant.Models;

namespace RiftAssistant.Services;

public class ChampSelectService
{
    private readonly LcuClient _lcuClient;

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public ChampSelectService(LcuClient lcuClient)
    {
        _lcuClient = lcuClient;
    }

    public async Task<ChampSelectSession?> GetSessionAsync()
    {
        string json = await _lcuClient.GetStringAsync(
            "/lol-champ-select/v1/session"
        );

        return JsonSerializer.Deserialize<ChampSelectSession>(
            json,
            _jsonOptions
        );
    }

    public async Task<ChampSelectTimer?> GetTimerAsync()
    {
        string json = await _lcuClient.GetStringAsync(
            "/lol-champ-select/v1/session/timer"
        );

        return JsonSerializer.Deserialize<ChampSelectTimer>(
            json,
            _jsonOptions
        );
    }

    public async Task<HashSet<int>> GetBannableChampionIdsAsync()
    {
        string json = await _lcuClient.GetStringAsync(
            "/lol-champ-select/v1/bannable-champion-ids"
        );

        var ids = JsonSerializer.Deserialize<List<int>>(
            json,
            _jsonOptions
        ) ?? new List<int>();

        return ids.ToHashSet();
    }

    public ChampSelectAction? GetMyActiveAction(
        ChampSelectSession session)
    {
        return session.Actions
            .SelectMany(actionGroup => actionGroup)
            .FirstOrDefault(action =>
                action.ActorCellId == session.LocalPlayerCellId &&
                action.IsInProgress &&
                !action.Completed
            );
    }
}
