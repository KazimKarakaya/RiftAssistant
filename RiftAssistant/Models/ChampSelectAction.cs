using System.Text.Json.Serialization;

namespace RiftAssistant.Models;

public class ChampSelectAction
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("actorCellId")]
    public int ActorCellId { get; set; }

    [JsonPropertyName("championId")]
    public int ChampionId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("isAllyAction")]
    public bool IsAllyAction { get; set; }

    [JsonPropertyName("isInProgress")]
    public bool IsInProgress { get; set; }

    [JsonPropertyName("pickTurn")]
    public int PickTurn { get; set; }

    [JsonPropertyName("duration")]
    public long Duration { get; set; }
}