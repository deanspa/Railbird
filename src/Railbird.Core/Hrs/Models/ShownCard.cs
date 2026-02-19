using System.Text.Json.Serialization;

namespace Railbird.Core.Hrs.Models;

public sealed class ShownCard
{
    [JsonPropertyName("seat_no")]
    public int SeatNo { get; set; }

    [JsonPropertyName("cards")]
    public List<string> Cards { get; set; } = new();
}
