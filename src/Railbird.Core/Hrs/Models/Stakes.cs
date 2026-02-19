using System.Text.Json.Serialization;

namespace Railbird.Core.Hrs.Models;

public sealed class Stakes
{
    [JsonPropertyName("small_blind")]
    public decimal SmallBlind { get; set; }

    [JsonPropertyName("big_blind")]
    public decimal BigBlind { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;
}
