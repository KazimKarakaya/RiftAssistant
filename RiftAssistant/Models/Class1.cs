using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace RiftAssistant.Models;

public class ChampSelectTeamMember
{
    [JsonPropertyName("cellId")]
    public int CellId { get; set; }

    [JsonPropertyName("championId")]
    public int ChampionId { get; set; }

    [JsonPropertyName("championPickIntent")]
    public int ChampionPickIntent { get; set; }

    [JsonPropertyName("assignedPosition")]
    public string AssignedPosition { get; set; } = string.Empty;
}
