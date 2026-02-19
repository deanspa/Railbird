using System.Globalization;
using System.Text.Json;
using Railbird.Core.Hrs.Models;
using Railbird.Core.Hrs.Validation;
using Railbird.Core.Rules;
using Railbird.Storage.Db;
using Railbird.Storage.Repos;

Console.WriteLine("Railbird Hand Recorder");
Console.WriteLine("This tool guides you through building a full HRS v1 hand JSON.");
Console.WriteLine();

var outputDir = Prompt.AskString("Output directory", "examples/hands/v1/generated", allowEmpty: false);
Directory.CreateDirectory(outputDir);

var handIdDefault = HandId.Generate();
var handId = Prompt.AskString($"Hand ID (format: {HandId.FormatDescription})", handIdDefault, allowEmpty: false);

var timestampUtc = Prompt.AskString("Timestamp UTC (ISO 8601)", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), allowEmpty: false);

var tableName = Prompt.AskString("Table name (optional)", "", allowEmpty: true);

var smallBlind = Prompt.AskDecimal("Small blind", 0.5m, min: 0.01m);
var bigBlind = Prompt.AskDecimal("Big blind", 1.0m, min: 0.01m);
var currency = Prompt.AskString("Currency", "GBP", allowEmpty: false);

var buttonSeat = Prompt.AskInt("Button seat (1-6)", 1, 1, 6);
var playerCount = Prompt.AskInt("Number of players (2-6)", 6, 2, 6);

var seats = Enumerable.Range(1, playerCount).ToList();
Console.WriteLine($"Using seats: {string.Join(", ", seats)}");
var heroSeat = Prompt.AskInt("Hero seat (choose from seats above)", seats[0], 1, 6);

var players = new List<Player>();
foreach (var seat in seats)
{
    var nameDefault = $"Player-Seat-{seat}";
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

var builder = new HandBuilder(players);
List<HandEvent> events = RulesEngine.Run(builder, players, seats, buttonSeat, smallBlind, bigBlind, heroSeat);

var outcome = OutcomeBuilder.BuildOutcome(players, builder.Pot, includeShownCardsDefault: false);

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

static class HandId
{
    public const string FormatDescription = "rb-v1-YYYYMMDD-HHMMSS-fff";

    public static string Generate()
    {
        return $"rb-v1-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}";
    }
}
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

    public static int AskOption(string label, List<string> options, int defaultIndex = 0)
    {
        while (true)
        {
            Console.WriteLine(label);
            for (var i = 0; i < options.Count; i++)
            {
                Console.WriteLine($"{i + 1}) {options[i]}");
            }

            var choice = AskInt("Choose option", defaultIndex + 1, 1, options.Count);
            return choice - 1;
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

static class RulesEngine
{
    public static List<HandEvent> Run(
        HandBuilder builder,
        List<Player> players,
        List<int> seats,
        int buttonSeat,
        decimal sb,
        decimal bb,
        int heroSeat)
    {
        var events = new List<HandEvent>();
        var displayNames = players.ToDictionary(p => p.SeatNo, p => p.DisplayName);
        var state = new NlheGameState(seats, buttonSeat, sb, bb, players.ToDictionary(p => p.SeatNo, p => p.StartingStack));

        events.Add(builder.AddEvent(EventType.POST_SB, Street.PREFLOP, state.SmallBlindSeat, sb, null, null, null));
        state.PostBlind(state.SmallBlindSeat, sb);

        events.Add(builder.AddEvent(EventType.POST_BB, Street.PREFLOP, state.BigBlindSeat, bb, null, null, null));
        state.PostBlind(state.BigBlindSeat, bb);

        var heroCards = Prompt.AskCards("Hero hole cards", 2, 2);
        events.Add(builder.AddEvent(EventType.DEAL_HOLE, Street.PREFLOP, heroSeat, null, null, heroCards, null));

        state.StartPreflop();

        while (!state.IsHandOver)
        {
            if (state.ShouldAdvanceStreet())
            {
                if (!state.AdvanceStreet())
                {
                    break;
                }
            }

            if (state.IsHandOver)
            {
                break;
            }

            if (state.NeedsBoardDeal)
            {
                AddBoardDeal(events, builder, state.CurrentStreet);
                state.MarkBoardDealt();
                continue;
            }

            var nextActor = state.GetNextActor();
            if (!nextActor.HasValue)
            {
                continue;
            }

            var actorSeat = nextActor.Value;
            var stack = state.Stacks[actorSeat];
            var committed = state.Committed[actorSeat];
            var callAmount = Math.Max(0m, state.CurrentBet - committed);

            var actions = state.GetLegalActions(actorSeat);
            var labels = actions.Select(a => BuildLabel(a, state, actorSeat)).ToList();
            var choiceIndex = Prompt.AskOption($"Action for seat {actorSeat} ({displayNames[actorSeat]})", labels);
            var choice = actions[choiceIndex];

            switch (choice)
            {
                case ActionKind.Check:
                    events.Add(builder.AddEvent(EventType.CHECK, state.CurrentStreet, actorSeat, null, null, null, null));
                    state.ApplyCheck(actorSeat);
                    break;
                case ActionKind.Fold:
                    events.Add(builder.AddEvent(EventType.FOLD, state.CurrentStreet, actorSeat, null, null, null, null));
                    state.ApplyFold(actorSeat);
                    break;
                case ActionKind.Call:
                    var callContribution = Math.Min(callAmount, stack);
                    events.Add(builder.AddEvent(EventType.CALL, state.CurrentStreet, actorSeat, callContribution, null, null, null));
                    state.ApplyCall(actorSeat, callContribution, isAllIn: callContribution >= stack);
                    break;
                case ActionKind.Bet:
                    var minBet = state.BigBlind;
                    var betAmount = Prompt.AskDecimal($"Bet amount (min {minBet})", minBet, min: minBet);
                    betAmount = Math.Min(betAmount, stack);
                    events.Add(builder.AddEvent(EventType.BET, state.CurrentStreet, actorSeat, betAmount, null, null, null));
                    state.ApplyBet(actorSeat, betAmount, isAllIn: betAmount >= stack);
                    break;
                case ActionKind.Raise:
                    var minRaiseTo = state.GetMinRaiseTo();
                    var maxRaiseTo = state.GetMaxRaiseTo(actorSeat);
                    var defaultRaiseTo = Math.Min(minRaiseTo, maxRaiseTo);
                    var raiseTo = Prompt.AskDecimal($"Raise to amount (min {minRaiseTo}, max {maxRaiseTo})", defaultRaiseTo, min: state.CurrentBet + 0.01m);
                    if (raiseTo > maxRaiseTo)
                    {
                        raiseTo = maxRaiseTo;
                    }
                    var raiseContribution = Math.Max(0m, raiseTo - committed);
                    var isAllInRaise = raiseContribution >= stack;
                    if (!isAllInRaise && raiseTo < minRaiseTo)
                    {
                        Console.WriteLine("Raise must meet min raise or be all-in. Try again.");
                        continue;
                    }
                    events.Add(builder.AddEvent(EventType.RAISE, state.CurrentStreet, actorSeat, raiseContribution, raiseTo, null, null));
                    state.ApplyRaise(actorSeat, raiseTo, raiseContribution, isAllIn: isAllInRaise);
                    break;
                case ActionKind.AllIn:
                    var allInTo = committed + stack;
                    if (state.CurrentBet == 0m)
                    {
                        events.Add(builder.AddEvent(EventType.ALL_IN, state.CurrentStreet, actorSeat, stack, allInTo, null, null));
                        state.ApplyBet(actorSeat, stack, isAllIn: true);
                    }
                    else if (allInTo <= state.CurrentBet)
                    {
                        events.Add(builder.AddEvent(EventType.ALL_IN, state.CurrentStreet, actorSeat, stack, allInTo, null, null));
                        state.ApplyCall(actorSeat, stack, isAllIn: true);
                    }
                    else
                    {
                        events.Add(builder.AddEvent(EventType.ALL_IN, state.CurrentStreet, actorSeat, stack, allInTo, null, null));
                        state.ApplyRaise(actorSeat, allInTo, stack, isAllIn: true);
                    }
                    break;
            }
        }

        return events;

        void AddBoardDeal(List<HandEvent> list, HandBuilder hb, Street street)
        {
            if (street == Street.FLOP)
            {
                var flop = Prompt.AskCards("Flop cards", 3, 3);
                list.Add(hb.AddEvent(EventType.DEAL_FLOP, street, null, null, null, flop, null));
            }
            else if (street == Street.TURN)
            {
                var turn = Prompt.AskCards("Turn card", 1, 1);
                list.Add(hb.AddEvent(EventType.DEAL_TURN, street, null, null, null, turn, null));
            }
            else if (street == Street.RIVER)
            {
                var river = Prompt.AskCards("River card", 1, 1);
                list.Add(hb.AddEvent(EventType.DEAL_RIVER, street, null, null, null, river, null));
            }
        }
    }

    private static string BuildLabel(ActionKind action, NlheGameState state, int seat)
    {
        var stack = state.Stacks[seat];
        var callAmount = Math.Min(state.GetCallAmount(seat), stack);
        return action switch
        {
            ActionKind.Check => "Check",
            ActionKind.Fold => "Fold",
            ActionKind.Call => $"Call ({callAmount})",
            ActionKind.Bet => $"Bet (min {state.BigBlind})",
            ActionKind.Raise => "Raise",
            ActionKind.AllIn => $"All-in ({stack})",
            _ => action.ToString()
        };
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









