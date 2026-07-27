using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace RiftAssistant.Models;

public class ChampSelectTimer
{
    [JsonPropertyName("adjustedTimeLeftInPhase")]
    public long AdjustedTimeLeftInPhase { get; set; }

    [JsonPropertyName("totalTimeInPhase")]
    public long TotalTimeInPhase { get; set; }

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = string.Empty;

    [JsonPropertyName("isInfinite")]
    public bool IsInfinite { get; set; }

    public double RemainingSeconds =>
        AdjustedTimeLeftInPhase / 1000.0;
}
