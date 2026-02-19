using System.Text.Json.Serialization;

namespace Railbird.Core.Hrs.Models;

public sealed class Outcome
{
    [JsonPropertyName("rake")]
    public decimal? Rake { get; set; }

    [JsonPropertyName("pots")]
    public List<Pot>? Pots { get; set; }

    [JsonPropertyName("hero_net")]
    public decimal? HeroNet { get; set; }

    [JsonPropertyName("shown_cards")]
    public List<ShownCard>? ShownCards { get; set; }
}
