using System.Text.Json.Serialization;

namespace RiftAssistant.Models;

public class Champion
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonPropertyName("squarePortraitPath")]
    public string SquarePortraitPath { get; set; } = string.Empty;

    // CommunityDragon'ın "latest" istemci asset yolunu kullanıyoruz.
    // ID tabanlı olduğu için şampiyon adı/alias değişikliklerinden etkilenmez.
    [JsonIgnore]
    public string IconUrl =>
        $"https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/champion-icons/{Id}.png";

    public override string ToString() => Name;
}
