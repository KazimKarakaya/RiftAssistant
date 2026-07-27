using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace RiftAssistant.Models;

public class ChampSelectBans
{
    [JsonPropertyName("myTeamBans")]
    public List<int> MyTeamBans { get; set; } = new();

    [JsonPropertyName("theirTeamBans")]
    public List<int> TheirTeamBans { get; set; } = new();
}
