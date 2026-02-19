using System.Text.Json.Serialization;

namespace Railbird.Core.Hrs.Models;

public sealed class Player
{
    [JsonPropertyName("seat_no")]
    public int SeatNo { get; set; }

    [JsonPropertyName("player_id")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("starting_stack")]
    public decimal StartingStack { get; set; }

    [JsonPropertyName("is_hero")]
    public bool IsHero { get; set; }
}
