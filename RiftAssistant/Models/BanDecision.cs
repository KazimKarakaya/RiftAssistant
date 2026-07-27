using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiftAssistant.Models;

public class BanDecision
{
    public int? ChampionId { get; set; }

    public int? PreferenceNumber { get; set; }

    public bool ShouldWait { get; set; }

    public string Reason { get; set; } = string.Empty;

    public bool HasChampion =>
        ChampionId.HasValue && ChampionId.Value > 0;
}
