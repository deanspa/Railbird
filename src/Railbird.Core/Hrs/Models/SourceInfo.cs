using System.Text.Json.Serialization;

namespace Railbird.Core.Hrs.Models;

public sealed class SourceInfo
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("raw_hand_history")]
    public string? RawHandHistory { get; set; }
}
