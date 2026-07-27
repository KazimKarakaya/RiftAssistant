using System.Collections.Generic;

namespace RiftAssistant.Models
{
    public sealed class RoleProfile
    {
        public string RoleKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public bool AutoBanEnabled { get; set; } = false;
        public int PrimaryBanChampionId { get; set; } = 0;
        public int SecondaryBanChampionId { get; set; } = 0;
        public int TertiaryBanChampionId { get; set; } = 0;
        public int AutoBanAtSeconds { get; set; } = 5;

        public bool AutoPickEnabled { get; set; } = false;
        public bool AutoHoverPickEnabled { get; set; } = true;
        public int PrimaryPickChampionId { get; set; } = 0;
        public int SecondaryPickChampionId { get; set; } = 0;
        public int TertiaryPickChampionId { get; set; } = 0;
        public int AutoPickAtSeconds { get; set; } = 5;
    }

    public sealed class RoleProfileSettings
    {
        public bool Enabled { get; set; } = false;
        public string SelectedRoleKey { get; set; } = "middle";
        public List<RoleProfile> Profiles { get; set; } = new();
    }
}
