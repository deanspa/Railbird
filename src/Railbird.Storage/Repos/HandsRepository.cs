using System;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Railbird.Core.Hrs.Models;
using Railbird.Storage.Db;

namespace Railbird.Storage.Repos;

public sealed class HandsRepository
{
    private readonly SqliteConnectionFactory _factory;

    public HandsRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public void UpsertHand(Hand hand, string rawJson)
    {
        using var connection = _factory.Open();
        using var tx = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT OR REPLACE INTO hands
(hand_id, timestamp_utc, game, max_seats, button_seat, small_blind, big_blind, currency, raw_json)
VALUES
($hand_id, $timestamp_utc, $game, $max_seats, $button_seat, $small_blind, $big_blind, $currency, $raw_json);
";
            cmd.Parameters.AddWithValue("$hand_id", hand.HandId);
            cmd.Parameters.AddWithValue("$timestamp_utc", hand.TimestampUtc);
            cmd.Parameters.AddWithValue("$game", hand.Game);
            cmd.Parameters.AddWithValue("$max_seats", hand.MaxSeats);
            cmd.Parameters.AddWithValue("$button_seat", hand.ButtonSeat);
            cmd.Parameters.AddWithValue("$small_blind", hand.Stakes.SmallBlind);
            cmd.Parameters.AddWithValue("$big_blind", hand.Stakes.BigBlind);
            cmd.Parameters.AddWithValue("$currency", hand.Stakes.Currency);
            cmd.Parameters.AddWithValue("$raw_json", rawJson);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM hand_players WHERE hand_id = $hand_id;";
            cmd.Parameters.AddWithValue("$hand_id", hand.HandId);
            cmd.ExecuteNonQuery();
        }

        foreach (var player in hand.Players)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO hand_players
(hand_id, seat_no, player_id, display_name, starting_stack, is_hero)
VALUES
($hand_id, $seat_no, $player_id, $display_name, $starting_stack, $is_hero);
";
            cmd.Parameters.AddWithValue("$hand_id", hand.HandId);
            cmd.Parameters.AddWithValue("$seat_no", player.SeatNo);
            cmd.Parameters.AddWithValue("$player_id", player.PlayerId);
            cmd.Parameters.AddWithValue("$display_name", (object?)player.DisplayName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$starting_stack", player.StartingStack);
            cmd.Parameters.AddWithValue("$is_hero", player.IsHero ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM hand_events WHERE hand_id = $hand_id;";
            cmd.Parameters.AddWithValue("$hand_id", hand.HandId);
            cmd.ExecuteNonQuery();
        }

        foreach (var ev in hand.Events)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO hand_events
(hand_id, seq, street, type, actor_seat, amount, to_amount, cards_json, pot_after, actor_stack_after, note)
VALUES
($hand_id, $seq, $street, $type, $actor_seat, $amount, $to_amount, $cards_json, $pot_after, $actor_stack_after, $note);
";
            cmd.Parameters.AddWithValue("$hand_id", hand.HandId);
            cmd.Parameters.AddWithValue("$seq", ev.Seq);
            cmd.Parameters.AddWithValue("$street", ev.Street.ToString());
            cmd.Parameters.AddWithValue("$type", ev.Type.ToString());
            cmd.Parameters.AddWithValue("$actor_seat", (object?)ev.ActorSeat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$amount", (object?)ev.Amount ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$to_amount", (object?)ev.ToAmount ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cards_json", ev.Cards == null ? DBNull.Value : JsonSerializer.Serialize(ev.Cards));
            cmd.Parameters.AddWithValue("$pot_after", (object?)ev.PotAfter ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$actor_stack_after", (object?)ev.ActorStackAfter ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$note", (object?)ev.Note ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public IReadOnlyList<HandListItem> ListHands(int take)
    {
        using var connection = _factory.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT
  h.hand_id,
  h.timestamp_utc,
  h.game,
  h.max_seats,
  h.button_seat,
  h.small_blind,
  h.big_blind,
  h.currency,
  (SELECT COUNT(*) FROM hand_players p WHERE p.hand_id = h.hand_id) AS player_count,
  (SELECT COUNT(*) FROM hand_events e WHERE e.hand_id = h.hand_id) AS event_count,
  (SELECT seat_no FROM hand_players p WHERE p.hand_id = h.hand_id AND p.is_hero = 1 LIMIT 1) AS hero_seat
FROM hands h
ORDER BY h.timestamp_utc DESC
LIMIT $take;
";
        cmd.Parameters.AddWithValue("$take", take);

        var results = new List<HandListItem>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new HandListItem
            {
                HandId = reader.GetString(0),
                TimestampUtc = reader.GetString(1),
                Game = reader.GetString(2),
                MaxSeats = reader.GetInt32(3),
                ButtonSeat = reader.GetInt32(4),
                SmallBlind = Convert.ToDecimal(reader.GetDouble(5)),
                BigBlind = Convert.ToDecimal(reader.GetDouble(6)),
                Currency = reader.GetString(7),
                PlayerCount = reader.GetInt32(8),
                EventCount = reader.GetInt32(9),
                HeroSeat = reader.IsDBNull(10) ? null : reader.GetInt32(10)
            });
        }

        return results;
    }
}

public sealed class HandListItem
{
    public string HandId { get; set; } = string.Empty;
    public string TimestampUtc { get; set; } = string.Empty;
    public string Game { get; set; } = string.Empty;
    public int MaxSeats { get; set; }
    public int ButtonSeat { get; set; }
    public decimal SmallBlind { get; set; }
    public decimal BigBlind { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public int EventCount { get; set; }
    public int? HeroSeat { get; set; }
}

