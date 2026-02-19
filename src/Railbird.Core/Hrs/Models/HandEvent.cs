using System.Text.Json.Serialization;

namespace Railbird.Core.Hrs.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Street
{
    PREFLOP,
    FLOP,
    TURN,
    RIVER,
    SHOWDOWN,
    SUMMARY
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventType
{
    POST_SB,
    POST_BB,
    POST_ANTE,
    DEAL_HOLE,
    DEAL_FLOP,
    DEAL_TURN,
    DEAL_RIVER,
    FOLD,
    CHECK,
    CALL,
    BET,
    RAISE,
    ALL_IN
}

public sealed class HandEvent
{
    [JsonPropertyName("seq")]
    public int Seq { get; set; }

    [JsonPropertyName("street")]
    public Street Street { get; set; }

    [JsonPropertyName("type")]
    public EventType Type { get; set; }

    [JsonPropertyName("actor_seat")]
    public int? ActorSeat { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("to_amount")]
    public decimal? ToAmount { get; set; }

    [JsonPropertyName("cards")]
    public List<string>? Cards { get; set; }

    [JsonPropertyName("pot_after")]
    public decimal? PotAfter { get; set; }

    [JsonPropertyName("actor_stack_after")]
    public decimal? ActorStackAfter { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}
