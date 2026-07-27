using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace RiftAssistant.Models;

public class ChampSelectSession
{
    [JsonPropertyName("localPlayerCellId")]
    public int LocalPlayerCellId { get; set; }

    [JsonPropertyName("actions")]
    public List<List<ChampSelectAction>> Actions { get; set; } = new();

    [JsonPropertyName("timer")]
    public ChampSelectTimer Timer { get; set; } = new();
    [JsonPropertyName("myTeam")]
    public List<ChampSelectTeamMember> MyTeam { get; set; } = new();

    [JsonPropertyName("bans")]
    public ChampSelectBans Bans { get; set; } = new();
}
