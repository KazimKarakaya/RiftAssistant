using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiftAssistant.Models;

namespace RiftAssistant.Services;

public class BanDecisionService
{
    public BanDecision Decide(
        IEnumerable<int> preferences,
        HashSet<int> bannableChampions,
        HashSet<int> bannedChampions,
        HashSet<int> teammateHoveredChampions,
        bool avoidTeammateHover,
        double remainingSeconds,
        double hoverFallbackAtSeconds)
    {
        var checkedChampions = new HashSet<int>();

        int[] preferenceArray =
            preferences.ToArray();

        for (int i = 0; i < preferenceArray.Length; i++)
        {
            int championId =
                preferenceArray[i];

            int preferenceNumber =
                i + 1;

            // Boş tercih.
            if (championId <= 0)
                continue;

            // Aynı champion birden fazla tercih olarak girilmiş.
            if (!checkedChampions.Add(championId))
                continue;

            // Zaten banlanmış.
            if (bannedChampions.Contains(championId))
                continue;


            // Takım arkadaşı şu anda gösteriyor.
            if (avoidTeammateHover &&
                teammateHoveredChampions.Contains(championId))
            {
                // Hâlâ zaman varsa 1. tercihten vazgeçme.
                if (remainingSeconds > hoverFallbackAtSeconds)
                {
                    return new BanDecision
                    {
                        ChampionId = null,
                        PreferenceNumber = preferenceNumber,
                        ShouldWait = true,
                        Reason =
                            $"{preferenceNumber}. tercih takım tarafından gösteriliyor."
                    };
                }

                // Artık fallback zamanı.
                // Sonraki tercihi kontrol et.
                continue;
            }

            return new BanDecision
            {
                ChampionId = championId,
                PreferenceNumber = preferenceNumber,
                ShouldWait = false,
                Reason =
                    $"{preferenceNumber}. tercih uygun."
            };
        }

        return new BanDecision
        {
            ChampionId = null,
            PreferenceNumber = null,
            ShouldWait = false,
            Reason =
                "Uygun ban tercihi bulunamadı."
        };
    }
}
