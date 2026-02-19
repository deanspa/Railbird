using System.Text.Json.Serialization;

namespace Railbird.Core.Hrs.Models;

public sealed class Hand
{
    [JsonPropertyName("hand_id")]
    public string HandId { get; set; } = string.Empty;

    [JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;

    [JsonPropertyName("max_seats")]
    public int MaxSeats { get; set; }

    [JsonPropertyName("timestamp_utc")]
    public string TimestampUtc { get; set; } = string.Empty;

    [JsonPropertyName("table_name")]
    public string? TableName { get; set; }

    [JsonPropertyName("stakes")]
    public Stakes Stakes { get; set; } = new();

    [JsonPropertyName("button_seat")]
    public int ButtonSeat { get; set; }

    [JsonPropertyName("source")]
    public SourceInfo? Source { get; set; }

    [JsonPropertyName("players")]
    public List<Player> Players { get; set; } = new();

    [JsonPropertyName("events")]
    public List<HandEvent> Events { get; set; } = new();

    [JsonPropertyName("outcome")]
    public Outcome? Outcome { get; set; }
}
