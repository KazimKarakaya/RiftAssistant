using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using RiftAssistant.Core;
using RiftAssistant.Models;

namespace RiftAssistant.Services;

public class ChampionService
{
    private readonly LcuClient _lcuClient;

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public ChampionService(LcuClient lcuClient)
    {
        _lcuClient = lcuClient;
    }

    public async Task<List<Champion>> GetChampionsAsync()
    {
        string json = await _lcuClient.GetStringAsync(
            "/lol-game-data/assets/v1/champion-summary.json"
        );

        var champions =
            JsonSerializer.Deserialize<List<Champion>>(
                json,
                _jsonOptions
            ) ?? new List<Champion>();

        return champions
            .Where(champion => champion.Id > 0)
            .OrderBy(champion => champion.Name)
            .ToList();
    }
}
