using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiftAssistant.Models;

namespace RiftAssistant.Services;

public class BanDecisionTestRunner
{
    private readonly BanDecisionService _service =
        new();

    public List<string> RunAll()
    {
        var results =
            new List<string>();

        Run(
            results,
            "1. tercih uygun",
            preferences: [91, 157, 238],
            bannable: [91, 157, 238],
            banned: [],
            hovered: [],
            remaining: 5,
            fallback: 2.5,
            expectedChampion: 91,
            expectedWait: false
        );

        Run(
            results,
            "1 banlı → 2 kullan",
            preferences: [91, 157, 238],
            bannable: [91, 157, 238],
            banned: [91],
            hovered: [],
            remaining: 5,
            fallback: 2.5,
            expectedChampion: 157,
            expectedWait: false
        );

        Run(
            results,
            "1 ve 2 banlı → 3 kullan",
            preferences: [91, 157, 238],
            bannable: [91, 157, 238],
            banned: [91, 157],
            hovered: [],
            remaining: 5,
            fallback: 2.5,
            expectedChampion: 238,
            expectedWait: false
        );

        Run(
            results,
            "1 hover + zaman var → bekle",
            preferences: [91, 157, 238],
            bannable: [91, 157, 238],
            banned: [],
            hovered: [91],
            remaining: 4,
            fallback: 2.5,
            expectedChampion: null,
            expectedWait: true
        );

        Run(
            results,
            "1 hover + fallback → 2 kullan",
            preferences: [91, 157, 238],
            bannable: [91, 157, 238],
            banned: [],
            hovered: [91],
            remaining: 2.4,
            fallback: 2.5,
            expectedChampion: 157,
            expectedWait: false
        );

        Run(
            results,
            "1 ve 2 hover + fallback → 3 kullan",
            preferences: [91, 157, 238],
            bannable: [91, 157, 238],
            banned: [],
            hovered: [91, 157],
            remaining: 2.4,
            fallback: 2.5,
            expectedChampion: 238,
            expectedWait: false
        );

        Run(
            results,
            "3 tercih de hover → ban yok",
            preferences: [91, 157, 238],
            bannable: [91, 157, 238],
            banned: [],
            hovered: [91, 157, 238],
            remaining: 2,
            fallback: 2.5,
            expectedChampion: null,
            expectedWait: false
        );

        Run(
            results,
            "1 bannable değil → 2 kullan",
            preferences: [91, 157, 238],
            bannable: [157, 238],
            banned: [],
            hovered: [],
            remaining: 5,
            fallback: 2.5,
            expectedChampion: 157,
            expectedWait: false
        );

        Run(
            results,
            "Tekrar eden tercihi atla",
            preferences: [91, 91, 238],
            bannable: [91, 238],
            banned: [91],
            hovered: [],
            remaining: 5,
            fallback: 2.5,
            expectedChampion: 238,
            expectedWait: false
        );

        Run(
            results,
            "Boş 1. tercih → 2 kullan",
            preferences: [0, 157, 238],
            bannable: [157, 238],
            banned: [],
            hovered: [],
            remaining: 5,
            fallback: 2.5,
            expectedChampion: 157,
            expectedWait: false
        );

        // Senin özellikle sorduğun senaryo:
        // Talon önce hover, sonra hover'dan çıkıyor.
        TestHoverReleased(results);

        int passed =
            results.Count(x =>
                x.StartsWith("✅"));

        int failed =
            results.Count(x =>
                x.StartsWith("❌"));

        results.Insert(
            0,
            $"TEST SONUCU: {passed} başarılı / {failed} başarısız"
        );

        return results;
    }

    private void TestHoverReleased(
        List<string> results)
    {
        int talon = 91;
        int yasuo = 157;
        int zed = 238;

        int[] preferences =
        [
            talon,
            yasuo,
            zed
        ];

        var bannable =
            new HashSet<int>
            {
                talon,
                yasuo,
                zed
            };

        // İlk kontrol:
        // Talon gösteriliyor.
        var firstDecision =
            _service.Decide(
                preferences,
                bannable,
                new HashSet<int>(),
                new HashSet<int>
                {
                    talon
                },
                true,
                4.0,
                2.5
            );

        // Sonraki session:
        // Oyuncu Talon'dan vazgeçti.
        var secondDecision =
            _service.Decide(
                preferences,
                bannable,
                new HashSet<int>(),
                new HashSet<int>(),
                true,
                3.5,
                2.5
            );

        bool success =
            firstDecision.ShouldWait &&
            !firstDecision.HasChampion &&
            secondDecision.ChampionId == talon &&
            !secondDecision.ShouldWait;

        results.Add(
            success
                ? "✅ Hover kaldırıldı → 1. tercih tekrar kullanılabilir"
                : "❌ Hover kaldırıldı testi"
        );
    }

    private void Run(
        List<string> results,
        string name,
        int[] preferences,
        int[] bannable,
        int[] banned,
        int[] hovered,
        double remaining,
        double fallback,
        int? expectedChampion,
        bool expectedWait)
    {
        BanDecision decision =
            _service.Decide(
                preferences,
                bannable.ToHashSet(),
                banned.ToHashSet(),
                hovered.ToHashSet(),
                true,
                remaining,
                fallback
            );

        bool success =
            decision.ChampionId ==
                expectedChampion &&
            decision.ShouldWait ==
                expectedWait;

        if (success)
        {
            results.Add(
                $"✅ {name}"
            );
        }
        else
        {
            results.Add(
                $"❌ {name} | " +
                $"Beklenen Champion={expectedChampion?.ToString() ?? "YOK"}, " +
                $"Gerçek={decision.ChampionId?.ToString() ?? "YOK"}, " +
                $"Bekle={decision.ShouldWait}"
            );
        }
    }
}
