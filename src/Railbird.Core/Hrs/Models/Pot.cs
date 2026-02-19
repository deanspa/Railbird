using System.Text.Json.Serialization;

namespace Railbird.Core.Hrs.Models;

public sealed class Pot
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("eligible_seats")]
    public List<int> EligibleSeats { get; set; } = new();

    [JsonPropertyName("winner_seats")]
    public List<int> WinnerSeats { get; set; } = new();

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}
