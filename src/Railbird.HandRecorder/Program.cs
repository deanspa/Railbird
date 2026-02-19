using System.Globalization;
using System.Text.Json;
using Railbird.Core.Hrs.Models;
using Railbird.Core.Hrs.Validation;
using Railbird.Storage.Db;
using Railbird.Storage.Repos;

Console.WriteLine("Railbird Hand Recorder");
Console.WriteLine("This tool guides you through building a full HRS v1 hand JSON.");
Console.WriteLine();

var outputDir = Prompt.AskString("Output directory", "examples/hands/v1/generated", allowEmpty: false);
Directory.CreateDirectory(outputDir);

var handIdDefault = $"v1-hand-{DateTime.UtcNow:yyyyMMddHHmmss}";
var handId = Prompt.AskString("Hand ID", handIdDefault, allowEmpty: false);

var timestampUtc = Prompt.AskString("Timestamp UTC (ISO 8601)", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), allowEmpty: false);

var tableName = Prompt.AskString("Table name (optional)", "", allowEmpty: true);

var smallBlind = Prompt.AskDecimal("Small blind", 0.5m, min: 0.01m);
var bigBlind = Prompt.AskDecimal("Big blind", 1.0m, min: 0.01m);
var currency = Prompt.AskString("Currency", "USD", allowEmpty: false);

var buttonSeat = Prompt.AskInt("Button seat (1-6)", 1, 1, 6);
var playerCount = Prompt.AskInt("Number of players (2-6)", 2, 2, 6);

var seats = Enumerable.Range(1, playerCount).ToList();
Console.WriteLine($"Using seats: {string.Join(", ", seats)}");
var heroSeat = Prompt.AskInt("Hero seat (choose from seats above)", seats[0], 1, 6);

var players = new List<Player>();
foreach (var seat in seats)
{
    var nameDefault = $"P{seat}";
    var displayName = Prompt.AskString($"Display name for seat {seat}", nameDefault, allowEmpty: false);
    var startingStack = Prompt.AskDecimal($"Starting stack for seat {seat}", 100m, min: 0m);

    players.Add(new Player
    {
        SeatNo = seat,
        PlayerId = $"P{seat}",
        DisplayName = displayName,
        StartingStack = startingStack,
        IsHero = seat == heroSeat
    });
}

Console.WriteLine();
Console.WriteLine("Scenario selection:");
Console.WriteLine("1) Simple preflop raise + fold");
Console.WriteLine("2) Showdown hand (flop/turn/river)");
Console.WriteLine("3) All-in preflop");
Console.WriteLine("4) Full coverage (all event types)");
Console.WriteLine("5) Manual event entry");

var scenarioChoice = Prompt.AskInt("Choose scenario (1-5)", 1, 1, 5);

var builder = new HandBuilder(players);
List<HandEvent> events = scenarioChoice switch
{
    1 => Scenarios.SimplePreflopFold(builder, buttonSeat, smallBlind, bigBlind, seats),
    2 => Scenarios.ShowdownHand(builder, buttonSeat, smallBlind, bigBlind, seats),
    3 => Scenarios.AllInPreflop(builder, buttonSeat, smallBlind, bigBlind, seats),
    4 => Scenarios.FullCoverage(builder, buttonSeat, smallBlind, bigBlind, seats),
    _ => Scenarios.Manual(builder, buttonSeat, smallBlind, bigBlind, seats)
};

var outcome = OutcomeBuilder.BuildOutcome(players, builder.Pot, includeShownCardsDefault: scenarioChoice == 2);

var hand = new Hand
{
    HandId = handId,
    Game = "NLHE",
    MaxSeats = 6,
    TimestampUtc = timestampUtc,
    TableName = string.IsNullOrWhiteSpace(tableName) ? null : tableName,
    Stakes = new Stakes
    {
        SmallBlind = smallBlind,
        BigBlind = bigBlind,
        Currency = currency
    },
    ButtonSeat = buttonSeat,
    Players = players,
    Events = events,
    Outcome = outcome
};

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true
};

var json = JsonSerializer.Serialize(hand, jsonOptions);
var validation = HrsValidator.Validate(json);
if (!validation.IsSuccess)
{
    Console.WriteLine("Schema validation failed:");
    foreach (var error in validation.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
    Console.WriteLine("Fix inputs and re-run.");
    return;
}

var fileName = $"{handId}.json";
var outputPath = Path.Combine(outputDir, fileName);
File.WriteAllText(outputPath, json);

Console.WriteLine();
Console.WriteLine($"Hand JSON written to: {outputPath}");

var importNow = Prompt.AskBool("Import into SQLite now?", false);
if (importNow)
{
    var connectionString = Config.GetConnectionString();
    var factory = new SqliteConnectionFactory(connectionString);
    MigrationRunner.EnsureDatabase(factory);
    var repo = new HandsRepository(factory);
    repo.UpsertHand(hand, json);
    Console.WriteLine("Imported into SQLite.");
}

Console.WriteLine("Done.");

static class Prompt
{
    public static string AskString(string label, string defaultValue, bool allowEmpty)
    {
        while (true)
        {
            Console.Write($"{label} [{defaultValue}]: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                if (!allowEmpty && string.IsNullOrWhiteSpace(defaultValue))
                {
                    Console.WriteLine("Value is required.");
                    continue;
                }

                return defaultValue;
            }

            return input.Trim();
        }
    }

    public static int AskInt(string label, int defaultValue, int min, int max)
    {
        while (true)
        {
            Console.Write($"{label} [{defaultValue}]: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max)
            {
                return value;
            }

            Console.WriteLine($"Enter an integer between {min} and {max}.");
        }
    }

    public static decimal AskDecimal(string label, decimal defaultValue, decimal min)
    {
        while (true)
        {
            Console.Write($"{label} [{defaultValue.ToString(CultureInfo.InvariantCulture)}]: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }

            if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= min)
            {
                return value;
            }

            Console.WriteLine($"Enter a number >= {min}.");
        }
    }

    public static bool AskBool(string label, bool defaultValue)
    {
        var defaultText = defaultValue ? "Y" : "N";
        while (true)
        {
            Console.Write($"{label} [y/n] ({defaultText}): ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }

            var trimmed = input.Trim().ToLowerInvariant();
            if (trimmed is "y" or "yes")
            {
                return true;
            }
            if (trimmed is "n" or "no")
            {
                return false;
            }

            Console.WriteLine("Enter 'y' or 'n'.");
        }
    }

    public static List<string> AskCards(string label, int min, int max)
    {
        while (true)
        {
            Console.Write($"{label} (comma-separated) [{min}-{max} cards]: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Cards are required.");
                continue;
            }

            var cards = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (cards.Count < min || cards.Count > max)
            {
                Console.WriteLine($"Enter between {min} and {max} cards.");
                continue;
            }

            if (cards.Any(c => !CardValidator.IsValid(c)))
            {
                Console.WriteLine("Cards must match pattern like 'As', 'Td', '7c'.");
                continue;
            }

            return cards;
        }
    }

    public static List<int> AskSeatList(string label, List<int> validSeats)
    {
        while (true)
        {
            Console.Write($"{label} (comma-separated seats): ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("At least one seat is required.");
                continue;
            }

            var seats = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var v) ? v : -1)
                .Where(v => v > 0)
                .Distinct()
                .ToList();

            if (seats.Count == 0 || seats.Any(s => !validSeats.Contains(s)))
            {
                Console.WriteLine($"Seats must be from: {string.Join(", ", validSeats)}");
                continue;
            }

            return seats;
        }
    }
}

static class CardValidator
{
    public static bool IsValid(string card)
    {
        if (card.Length != 2)
        {
            return false;
        }

        const string ranks = "AKQJT98765432";
        const string suits = "shdc";
        return ranks.Contains(card[0]) && suits.Contains(char.ToLowerInvariant(card[1]));
    }
}

sealed class HandBuilder
{
    private readonly Dictionary<int, decimal> _stacks;
    private readonly Dictionary<int, decimal> _committed;
    private Street _currentStreet;
    private decimal _currentBet;
    private int _seq;

    public decimal Pot { get; private set; }

    public HandBuilder(IEnumerable<Player> players)
    {
        _stacks = players.ToDictionary(p => p.SeatNo, p => p.StartingStack);
        _committed = players.ToDictionary(p => p.SeatNo, _ => 0m);
        _currentStreet = Street.PREFLOP;
        _currentBet = 0m;
        _seq = 0;
        Pot = 0m;
    }

    public void StartStreet(Street street)
    {
        if (street == _currentStreet)
        {
            return;
        }

        _currentStreet = street;
        _currentBet = 0m;
        foreach (var seat in _committed.Keys.ToList())
        {
            _committed[seat] = 0m;
        }
    }

    public HandEvent AddEvent(EventType type, Street street, int? actorSeat, decimal? amount, decimal? toAmount, List<string>? cards, string? note)
    {
        StartStreet(street);

        var finalAmount = amount;
        if (type == EventType.RAISE && actorSeat.HasValue && toAmount.HasValue)
        {
            var committed = _committed[actorSeat.Value];
            finalAmount ??= Math.Max(0m, toAmount.Value - committed);
            _currentBet = toAmount.Value;
        }
        else if (type == EventType.CALL && actorSeat.HasValue)
        {
            var committed = _committed[actorSeat.Value];
            finalAmount ??= Math.Max(0m, _currentBet - committed);
        }
        else if (type == EventType.BET && amount.HasValue)
        {
            _currentBet = amount.Value;
        }
        else if (type == EventType.ALL_IN && toAmount.HasValue)
        {
            _currentBet = Math.Max(_currentBet, toAmount.Value);
        }

        if (finalAmount.HasValue)
        {
            Pot += finalAmount.Value;
            if (actorSeat.HasValue)
            {
                _stacks[actorSeat.Value] -= finalAmount.Value;
                _committed[actorSeat.Value] += finalAmount.Value;
            }
        }

        return new HandEvent
        {
            Seq = ++_seq,
            Street = street,
            Type = type,
            ActorSeat = actorSeat,
            Amount = finalAmount,
            ToAmount = toAmount,
            Cards = cards,
            PotAfter = Pot,
            ActorStackAfter = actorSeat.HasValue ? _stacks[actorSeat.Value] : null,
            Note = note
        };
    }
}

static class OutcomeBuilder
{
    public static Outcome? BuildOutcome(List<Player> players, decimal pot, bool includeShownCardsDefault)
    {
        var includeOutcome = Prompt.AskBool("Include outcome section?", true);
        if (!includeOutcome)
        {
            return null;
        }

        var rake = Prompt.AskDecimal("Rake", 0m, min: 0m);
        var eligibleSeats = players.Select(p => p.SeatNo).ToList();
        var winnerSeats = Prompt.AskSeatList("Winner seats", eligibleSeats);
        var heroNet = Prompt.AskDecimal("Hero net (optional, use 0 if unknown)", 0m, min: 0m);

        var outcome = new Outcome
        {
            Rake = rake,
            HeroNet = heroNet,
            Pots = new List<Pot>
            {
                new Pot
                {
                    Amount = pot,
                    EligibleSeats = eligibleSeats,
                    WinnerSeats = winnerSeats,
                    Note = "Generated by hand recorder"
                }
            }
        };

        var includeShown = Prompt.AskBool("Include shown cards?", includeShownCardsDefault);
        if (includeShown)
        {
            var shown = new List<ShownCard>();
            foreach (var seat in eligibleSeats)
            {
                var includeSeat = Prompt.AskBool($"Show cards for seat {seat}?", false);
                if (!includeSeat)
                {
                    continue;
                }

                var cards = Prompt.AskCards($"Cards for seat {seat}", 2, 2);
                shown.Add(new ShownCard
                {
                    SeatNo = seat,
                    Cards = cards
                });
            }

            if (shown.Count > 0)
            {
                outcome.ShownCards = shown;
            }
        }

        return outcome;
    }
}

static class Scenarios
{
    public static List<HandEvent> SimplePreflopFold(HandBuilder builder, int buttonSeat, decimal sb, decimal bb, List<int> seats)
    {
        var events = new List<HandEvent>();
        var sbSeat = seats[0];
        var bbSeat = seats.Count > 1 ? seats[1] : seats[0];

        events.Add(builder.AddEvent(EventType.POST_SB, Street.PREFLOP, sbSeat, sb, null, null, null));
        events.Add(builder.AddEvent(EventType.POST_BB, Street.PREFLOP, bbSeat, bb, null, null, null));
        events.Add(builder.AddEvent(EventType.DEAL_HOLE, Street.PREFLOP, sbSeat, null, null, Prompt.AskCards("Hero hole cards", 2, 2), null));
        events.Add(builder.AddEvent(EventType.RAISE, Street.PREFLOP, sbSeat, bb * 2, bb * 3, null, null));
        events.Add(builder.AddEvent(EventType.FOLD, Street.PREFLOP, bbSeat, null, null, null, "Folded preflop"));
        return events;
    }

    public static List<HandEvent> ShowdownHand(HandBuilder builder, int buttonSeat, decimal sb, decimal bb, List<int> seats)
    {
        var events = new List<HandEvent>();
        var sbSeat = seats[0];
        var bbSeat = seats.Count > 1 ? seats[1] : seats[0];

        events.Add(builder.AddEvent(EventType.POST_SB, Street.PREFLOP, sbSeat, sb, null, null, null));
        events.Add(builder.AddEvent(EventType.POST_BB, Street.PREFLOP, bbSeat, bb, null, null, null));
        events.Add(builder.AddEvent(EventType.DEAL_HOLE, Street.PREFLOP, sbSeat, null, null, Prompt.AskCards("Hero hole cards", 2, 2), null));
        events.Add(builder.AddEvent(EventType.CALL, Street.PREFLOP, sbSeat, bb, null, null, null));
        events.Add(builder.AddEvent(EventType.CHECK, Street.PREFLOP, bbSeat, null, null, null, null));

        events.Add(builder.AddEvent(EventType.DEAL_FLOP, Street.FLOP, null, null, null, Prompt.AskCards("Flop cards", 3, 3), null));
        events.Add(builder.AddEvent(EventType.BET, Street.FLOP, bbSeat, bb, null, null, null));
        events.Add(builder.AddEvent(EventType.CALL, Street.FLOP, sbSeat, bb, null, null, null));

        events.Add(builder.AddEvent(EventType.DEAL_TURN, Street.TURN, null, null, null, Prompt.AskCards("Turn card", 1, 1), null));
        events.Add(builder.AddEvent(EventType.CHECK, Street.TURN, sbSeat, null, null, null, null));
        events.Add(builder.AddEvent(EventType.CHECK, Street.TURN, bbSeat, null, null, null, null));

        events.Add(builder.AddEvent(EventType.DEAL_RIVER, Street.RIVER, null, null, null, Prompt.AskCards("River card", 1, 1), null));
        events.Add(builder.AddEvent(EventType.BET, Street.RIVER, sbSeat, bb, null, null, null));
        events.Add(builder.AddEvent(EventType.CALL, Street.RIVER, bbSeat, bb, null, null, null));

        return events;
    }

    public static List<HandEvent> AllInPreflop(HandBuilder builder, int buttonSeat, decimal sb, decimal bb, List<int> seats)
    {
        var events = new List<HandEvent>();
        var sbSeat = seats[0];
        var bbSeat = seats.Count > 1 ? seats[1] : seats[0];

        events.Add(builder.AddEvent(EventType.POST_SB, Street.PREFLOP, sbSeat, sb, null, null, null));
        events.Add(builder.AddEvent(EventType.POST_BB, Street.PREFLOP, bbSeat, bb, null, null, null));
        events.Add(builder.AddEvent(EventType.DEAL_HOLE, Street.PREFLOP, sbSeat, null, null, Prompt.AskCards("Hero hole cards", 2, 2), null));

        var allInAmount = Prompt.AskDecimal("All-in amount", bb * 20, min: bb);
        events.Add(builder.AddEvent(EventType.ALL_IN, Street.PREFLOP, sbSeat, allInAmount, allInAmount, null, null));
        events.Add(builder.AddEvent(EventType.CALL, Street.PREFLOP, bbSeat, allInAmount, null, null, null));

        return events;
    }

    public static List<HandEvent> FullCoverage(HandBuilder builder, int buttonSeat, decimal sb, decimal bb, List<int> seats)
    {
        var events = new List<HandEvent>();
        var sbSeat = seats[0];
        var bbSeat = seats.Count > 1 ? seats[1] : seats[0];
        var thirdSeat = seats.Count > 2 ? seats[2] : bbSeat;

        var ante = Prompt.AskDecimal("Ante amount", 0.1m, min: 0m);
        foreach (var seat in seats)
        {
            if (ante > 0m)
            {
                events.Add(builder.AddEvent(EventType.POST_ANTE, Street.PREFLOP, seat, ante, null, null, null));
            }
        }

        events.Add(builder.AddEvent(EventType.POST_SB, Street.PREFLOP, sbSeat, sb, null, null, null));
        events.Add(builder.AddEvent(EventType.POST_BB, Street.PREFLOP, bbSeat, bb, null, null, null));
        events.Add(builder.AddEvent(EventType.DEAL_HOLE, Street.PREFLOP, sbSeat, null, null, Prompt.AskCards("Hero hole cards", 2, 2), null));

        events.Add(builder.AddEvent(EventType.RAISE, Street.PREFLOP, sbSeat, bb * 2, bb * 3, null, null));
        events.Add(builder.AddEvent(EventType.CALL, Street.PREFLOP, bbSeat, bb * 2, null, null, null));

        events.Add(builder.AddEvent(EventType.DEAL_FLOP, Street.FLOP, null, null, null, Prompt.AskCards("Flop cards", 3, 3), null));
        events.Add(builder.AddEvent(EventType.CHECK, Street.FLOP, bbSeat, null, null, null, null));
        events.Add(builder.AddEvent(EventType.BET, Street.FLOP, sbSeat, bb, null, null, null));
        events.Add(builder.AddEvent(EventType.RAISE, Street.FLOP, bbSeat, bb, bb * 3, null, null));
        events.Add(builder.AddEvent(EventType.CALL, Street.FLOP, sbSeat, bb * 2, null, null, null));

        events.Add(builder.AddEvent(EventType.DEAL_TURN, Street.TURN, null, null, null, Prompt.AskCards("Turn card", 1, 1), null));
        events.Add(builder.AddEvent(EventType.CHECK, Street.TURN, sbSeat, null, null, null, null));
        events.Add(builder.AddEvent(EventType.CHECK, Street.TURN, bbSeat, null, null, null, null));

        events.Add(builder.AddEvent(EventType.DEAL_RIVER, Street.RIVER, null, null, null, Prompt.AskCards("River card", 1, 1), null));
        var allInAmount = Prompt.AskDecimal("All-in amount", bb * 10, min: bb);
        events.Add(builder.AddEvent(EventType.ALL_IN, Street.RIVER, bbSeat, allInAmount, allInAmount, null, "All-in river bet"));
        events.Add(builder.AddEvent(EventType.FOLD, Street.RIVER, sbSeat, null, null, null, "Folded to all-in"));

        return events;
    }

    public static List<HandEvent> Manual(HandBuilder builder, int buttonSeat, decimal sb, decimal bb, List<int> seats)
    {
        var events = new List<HandEvent>();

        var includePosts = Prompt.AskBool("Auto-add blinds (SB/BB)?", true);
        if (includePosts)
        {
            var sbSeat = seats[0];
            var bbSeat = seats.Count > 1 ? seats[1] : seats[0];
            events.Add(builder.AddEvent(EventType.POST_SB, Street.PREFLOP, sbSeat, sb, null, null, null));
            events.Add(builder.AddEvent(EventType.POST_BB, Street.PREFLOP, bbSeat, bb, null, null, null));
        }

        var includeDeal = Prompt.AskBool("Add DEAL_HOLE event for hero?", true);
        if (includeDeal)
        {
            var heroSeat = seats[0];
            events.Add(builder.AddEvent(EventType.DEAL_HOLE, Street.PREFLOP, heroSeat, null, null, Prompt.AskCards("Hero hole cards", 2, 2), null));
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Add event type:");
            var eventTypes = Enum.GetValues<EventType>().ToList();
            for (var i = 0; i < eventTypes.Count; i++)
            {
                Console.WriteLine($"{i + 1}) {eventTypes[i]}");
            }

            var choice = Prompt.AskInt("Choose event type", 1, 1, eventTypes.Count);
            var type = eventTypes[choice - 1];

            var street = (Street)Prompt.AskInt("Street (1=PREFLOP,2=FLOP,3=TURN,4=RIVER,5=SHOWDOWN,6=SUMMARY)", 1, 1, 6) - 1;

            int? actorSeat = null;
            if (type != EventType.DEAL_FLOP && type != EventType.DEAL_TURN && type != EventType.DEAL_RIVER)
            {
                actorSeat = Prompt.AskInt("Actor seat (1-6)", seats[0], 1, 6);
            }

            decimal? amount = null;
            decimal? toAmount = null;
            if (type is EventType.POST_SB or EventType.POST_BB or EventType.POST_ANTE or EventType.BET or EventType.CALL or EventType.RAISE or EventType.ALL_IN)
            {
                amount = Prompt.AskDecimal("Amount", 0m, min: 0m);
            }

            if (type == EventType.RAISE || type == EventType.ALL_IN)
            {
                toAmount = Prompt.AskDecimal("To amount", amount ?? 0m, min: 0m);
            }

            List<string>? cards = null;
            if (type == EventType.DEAL_HOLE)
            {
                cards = Prompt.AskCards("Hole cards", 2, 2);
            }
            else if (type == EventType.DEAL_FLOP)
            {
                cards = Prompt.AskCards("Flop cards", 3, 3);
            }
            else if (type == EventType.DEAL_TURN || type == EventType.DEAL_RIVER)
            {
                cards = Prompt.AskCards("Board card", 1, 1);
            }

            var note = Prompt.AskString("Note (optional)", "", allowEmpty: true);

            events.Add(builder.AddEvent(type, street, actorSeat, amount, toAmount, cards, string.IsNullOrWhiteSpace(note) ? null : note));

            if (!Prompt.AskBool("Add another event?", true))
            {
                break;
            }
        }

        return events;
    }
}

static class Config
{
    public static string GetConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("RAILBIRD_DB");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        var appsettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(appsettings))
        {
            var json = JsonDocument.Parse(File.ReadAllText(appsettings));
            if (json.RootElement.TryGetProperty("ConnectionStrings", out var cs)
                && cs.TryGetProperty("RailbirdDb", out var db))
            {
                return db.GetString() ?? ".local/railbird.db";
            }
        }

        return ".local/railbird.db";
    }
}
