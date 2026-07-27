using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiftAssistant.Models;

public class AppSettings
{
    public bool AutoAcceptEnabled { get; set; } = true;
    public int AutoAcceptDelaySeconds { get; set; } = 5;
    public bool AutoBanEnabled { get; set; } = false;
    public int BanChampionId { get; set; } = 157;
    public bool TopMost { get; set; } = true;
    public int AutoBanAtSeconds { get; set; } = 5;
    public int PrimaryBanChampionId { get; set; } = 0;
    public int SecondaryBanChampionId { get; set; } = 0;
    public int TertiaryBanChampionId { get; set; } = 0;
    public bool AvoidTeammateHover { get; set; } = true;
    public bool AutoPickEnabled { get; set; } = false;
    public int PrimaryPickChampionId { get; set; } = 0;
    public int SecondaryPickChampionId { get; set; } = 0;
    public int TertiaryPickChampionId { get; set; } = 0;
    public int AutoPickAtSeconds { get; set; } = 5;
    public bool AutoHoverPickEnabled { get; set; } = true;
}
