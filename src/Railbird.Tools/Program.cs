using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Railbird.Core.Hrs.Models;
using Tomlyn;
using Tomlyn.Model;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = args[0].ToLowerInvariant();
if (command is not "phh-to-hrs" and not "phh-to-hrs-git")
{
    PrintUsage();
    return 1;
}

var inputPath = GetOption(args, "--input");
var outputPath = GetOption(args, "--output");
var currency = GetOption(args, "--currency") ?? "USD";
var playersText = GetOption(args, "--players");
var playerCount = string.IsNullOrWhiteSpace(playersText) ? 6 : int.Parse(playersText, CultureInfo.InvariantCulture);
var maxCountText = GetOption(args, "--max");
var maxCount = string.IsNullOrWhiteSpace(maxCountText) ? int.MaxValue : int.Parse(maxCountText, CultureInfo.InvariantCulture);

if (string.IsNullOrWhiteSpace(outputPath))
{
    Console.WriteLine("--output is required.");
    PrintUsage();
    return 1;
}

Directory.CreateDirectory(outputPath);
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
var converted = 0;
var skipped = 0;
var processed = 0;

if (command == "phh-to-hrs")
{
    if (string.IsNullOrWhiteSpace(inputPath))
    {
        Console.WriteLine("--input is required.");
        PrintUsage();
        return 1;
    }

    var inputFiles = CollectInputFiles(inputPath).ToList();
    if (inputFiles.Count == 0)
    {
        Console.WriteLine("No .phh/.phhs files found at input path.");
        return 1;
    }

    foreach (var file in inputFiles)
    {
        if (converted >= maxCount)
        {
            break;
        }

        processed++;
        var text = File.ReadAllText(file);
        ConvertToml(text, Path.GetFileNameWithoutExtension(file), currency, playerCount, maxCount, outputPath, jsonOptions, ref converted, ref skipped);
    }
}
else
{
    var repoPath = GetOption(args, "--repo");
    var pathsFile = GetOption(args, "--paths");
    if (string.IsNullOrWhiteSpace(repoPath) || string.IsNullOrWhiteSpace(pathsFile))
    {
        Console.WriteLine("--repo and --paths are required for phh-to-hrs-git.");
        PrintUsage();
        return 1;
    }

    if (!File.Exists(pathsFile))
    {
        Console.WriteLine("Paths file not found.");
        return 1;
    }

    var paths = File.ReadAllLines(pathsFile)
        .Select(p => p.Trim())
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .ToList();

    foreach (var path in paths)
    {
        if (converted >= maxCount)
        {
            break;
        }

        processed++;
        var text = GitShow(repoPath, path);
        if (text == null)
        {
            skipped++;
            continue;
        }

        ConvertToml(text, Path.GetFileNameWithoutExtension(path), currency, playerCount, maxCount, outputPath, jsonOptions, ref converted, ref skipped);
    }
}

Console.WriteLine($"Processed {processed} files. Converted {converted} hands. Skipped {skipped}.");
return 0;

static IEnumerable<string> CollectInputFiles(string inputPath)
{
    if (File.Exists(inputPath))
    {
        yield return inputPath;
        yield break;
    }

    if (Directory.Exists(inputPath))
    {
        foreach (var file in Directory.GetFiles(inputPath, "*.phh", SearchOption.TopDirectoryOnly))
        {
            yield return file;
        }
        foreach (var file in Directory.GetFiles(inputPath, "*.phhs", SearchOption.TopDirectoryOnly))
        {
            yield return file;
        }
    }
}

static void ConvertToml(
    string tomlText,
    string baseId,
    string currency,
    int playerCount,
    int maxCount,
    string outputPath,
    JsonSerializerOptions jsonOptions,
    ref int converted,
    ref int skipped)
{
    var parse = Toml.Parse(tomlText);
    if (parse.HasErrors)
    {
        skipped++;
        return;
    }

    var model = parse.ToModel();
    if (model is not TomlTable table)
    {
        skipped++;
        return;
    }

    if (IsSingleHand(table))
    {
        if (converted >= maxCount)
        {
            return;
        }

        if (!TryConvertHand(table, baseId, currency, playerCount, 0, out var hand))
        {
            skipped++;
            return;
        }

        WriteHand(outputPath, jsonOptions, hand);
        converted++;
        return;
    }

    var subTables = table
        .Where(kvp => kvp.Value is TomlTable)
        .Select(kvp => (Key: kvp.Key, Table: (TomlTable)kvp.Value!))
        .OrderBy(kvp => int.TryParse(kvp.Key, out var v) ? v : int.MaxValue)
        .ToList();

    foreach (var entry in subTables)
    {
        if (converted >= maxCount)
        {
            break;
        }

        if (!TryConvertHand(entry.Table, baseId, currency, playerCount, entry.Key, out var hand))
        {
            skipped++;
            continue;
        }

        WriteHand(outputPath, jsonOptions, hand);
        converted++;
    }
}

static bool IsSingleHand(TomlTable table)
{
    return table.ContainsKey("variant") && table.ContainsKey("actions");
}

static bool TryConvertHand(TomlTable table, string baseId, string currency, int playerCount, object? subKey, out Hand hand)
{
    hand = new Hand();

    var variant = GetString(table, "variant");
    if (!string.Equals(variant, "NT", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var antes = GetDecimalArray(table, "antes");
    if (antes.Any(a => a > 0m))
    {
        return false;
    }

    var blinds = GetDecimalArray(table, "blinds_or_straddles");
    var nonZeroBlinds = blinds.Where(b => b > 0m).Distinct().ToList();
    if (nonZeroBlinds.Count < 2)
    {
        return false;
    }

    var smallBlind = nonZeroBlinds.Min();
    var bigBlind = nonZeroBlinds.Max();

    var startingStacks = GetDecimalArray(table, "starting_stacks");
    if (startingStacks.Count != playerCount)
    {
        return false;
    }

    var actions = GetStringArray(table, "actions");
    if (actions.Count == 0)
    {
        return false;
    }

    var seats = Enumerable.Range(1, startingStacks.Count).ToList();
    var heroSeat = 1;

    var events = BuildEvents(actions, seats, heroSeat, smallBlind, bigBlind);
    if (events.Count == 0)
    {
        return false;
    }

    var handIdSuffix = GetString(table, "hand") ?? subKey?.ToString() ?? "0";
    var timestampUtc = BuildTimestamp(table);

    hand = new Hand
    {
        HandId = $"phh-{baseId}-{handIdSuffix}",
        Game = "NLHE",
        MaxSeats = 6,
        TimestampUtc = timestampUtc,
        Stakes = new Stakes
        {
            SmallBlind = smallBlind,
            BigBlind = bigBlind,
            Currency = currency
        },
        ButtonSeat = 1,
        Players = seats.Select(seat => new Player
        {
            SeatNo = seat,
            PlayerId = $"P{seat}",
            DisplayName = $"Player-Seat-{seat}",
            StartingStack = startingStacks[seat - 1],
            IsHero = seat == heroSeat
        }).ToList(),
        Events = events
    };

    return true;
}

static void WriteHand(string outputPath, JsonSerializerOptions jsonOptions, Hand hand)
{
    var fileName = $"{hand.HandId}.json";
    var fullPath = Path.Combine(outputPath, fileName);
    File.WriteAllText(fullPath, JsonSerializer.Serialize(hand, jsonOptions));
}

static string BuildTimestamp(TomlTable table)
{
    var year = GetInt(table, "year");
    var month = GetInt(table, "month");
    var day = GetInt(table, "day");
    var time = GetString(table, "time");

    if (year.HasValue && month.HasValue && day.HasValue && !string.IsNullOrWhiteSpace(time))
    {
        if (TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out var ts))
        {
            var dt = new DateTime(year.Value, month.Value, day.Value, ts.Hours, ts.Minutes, ts.Seconds, DateTimeKind.Utc);
            return dt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
    }

    return new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}

static List<HandEvent> BuildEvents(List<string> actions, List<int> seats, int heroSeat, decimal sb, decimal bb)
{
    var events = new List<HandEvent>();
    var street = Street.PREFLOP;
    var committed = seats.ToDictionary(s => s, _ => 0m);
    var currentBet = 0m;
    var seq = 0;

    events.Add(new HandEvent { Seq = ++seq, Street = Street.PREFLOP, Type = EventType.POST_SB, ActorSeat = seats[0], Amount = sb });
    events.Add(new HandEvent { Seq = ++seq, Street = Street.PREFLOP, Type = EventType.POST_BB, ActorSeat = seats.Count > 1 ? seats[1] : seats[0], Amount = bb });
    currentBet = bb;
    committed[seats[0]] = sb;
    if (seats.Count > 1)
    {
        committed[seats[1]] = bb;
    }

    void ResetStreet()
    {
        currentBet = 0m;
        foreach (var seat in seats)
        {
            committed[seat] = 0m;
        }
    }

    foreach (var raw in actions)
    {
        var trimmed = raw.Split('#')[0].Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            continue;
        }

        if (trimmed.StartsWith("d db ", StringComparison.OrdinalIgnoreCase))
        {
            var cardText = trimmed[5..].Trim();
            var cards = ParseCards(cardText);
            if (cards.Count == 3)
            {
                street = Street.FLOP;
                ResetStreet();
                events.Add(new HandEvent { Seq = ++seq, Street = street, Type = EventType.DEAL_FLOP, Cards = cards });
            }
            else if (cards.Count == 1 && street == Street.FLOP)
            {
                street = Street.TURN;
                ResetStreet();
                events.Add(new HandEvent { Seq = ++seq, Street = street, Type = EventType.DEAL_TURN, Cards = cards });
            }
            else if (cards.Count == 1 && street == Street.TURN)
            {
                street = Street.RIVER;
                ResetStreet();
                events.Add(new HandEvent { Seq = ++seq, Street = street, Type = EventType.DEAL_RIVER, Cards = cards });
            }
            continue;
        }

        var dealMatch = Regex.Match(trimmed, "^d dh p(\\d+) (.+)$", RegexOptions.IgnoreCase);
        if (dealMatch.Success)
        {
            var seat = int.Parse(dealMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            if (seat != heroSeat)
            {
                continue;
            }

            var cards = ParseCards(dealMatch.Groups[2].Value);
            if (cards.Any(c => c.Contains("?", StringComparison.Ordinal)))
            {
                continue;
            }

            events.Add(new HandEvent { Seq = ++seq, Street = Street.PREFLOP, Type = EventType.DEAL_HOLE, ActorSeat = seat, Cards = cards });
            continue;
        }

        var cbrMatch = Regex.Match(trimmed, "^p(\\d+) cbr (.+)$", RegexOptions.IgnoreCase);
        if (cbrMatch.Success)
        {
            var seat = int.Parse(cbrMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!TryParseDecimal(cbrMatch.Groups[2].Value, out var toAmount))
            {
                continue;
            }

            if (currentBet == 0m)
            {
                var amount = toAmount;
                committed[seat] += amount;
                currentBet = toAmount;
                events.Add(new HandEvent { Seq = ++seq, Street = street, Type = EventType.BET, ActorSeat = seat, Amount = amount });
            }
            else
            {
                var amount = Math.Max(0m, toAmount - committed[seat]);
                committed[seat] += amount;
                currentBet = toAmount;
                events.Add(new HandEvent { Seq = ++seq, Street = street, Type = EventType.RAISE, ActorSeat = seat, Amount = amount, ToAmount = toAmount });
            }
            continue;
        }

        var ccMatch = Regex.Match(trimmed, "^p(\\d+) cc$", RegexOptions.IgnoreCase);
        if (ccMatch.Success)
        {
            var seat = int.Parse(ccMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            if (committed[seat] >= currentBet)
            {
                events.Add(new HandEvent { Seq = ++seq, Street = street, Type = EventType.CHECK, ActorSeat = seat });
            }
            else
            {
                var amount = currentBet - committed[seat];
                committed[seat] = currentBet;
                events.Add(new HandEvent { Seq = ++seq, Street = street, Type = EventType.CALL, ActorSeat = seat, Amount = amount });
            }
            continue;
        }

        var foldMatch = Regex.Match(trimmed, "^p(\\d+) f$", RegexOptions.IgnoreCase);
        if (foldMatch.Success)
        {
            var seat = int.Parse(foldMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            events.Add(new HandEvent { Seq = ++seq, Street = street, Type = EventType.FOLD, ActorSeat = seat });
            continue;
        }
    }

    return events;
}

static List<string> ParseCards(string raw)
{
    var cleaned = raw.Replace(" ", string.Empty).Replace(",", string.Empty);
    var cards = new List<string>();
    for (var i = 0; i + 1 < cleaned.Length; i += 2)
    {
        cards.Add(cleaned.Substring(i, 2));
    }
    return cards;
}

static bool TryParseDecimal(string input, out decimal value)
{
    return decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}

static string? GetOption(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return null;
}

static string? GetString(TomlTable table, string key)
{
    return table.TryGetValue(key, out var value) ? value?.ToString() : null;
}

static int? GetInt(TomlTable table, string key)
{
    if (!table.TryGetValue(key, out var value) || value is null)
    {
        return null;
    }

    return value switch
    {
        long l => (int)l,
        int i => i,
        _ => int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null
    };
}

static List<decimal> GetDecimalArray(TomlTable table, string key)
{
    var list = new List<decimal>();
    if (!table.TryGetValue(key, out var value) || value is not TomlArray arr)
    {
        return list;
    }

    foreach (var item in arr)
    {
        if (item is null)
        {
            continue;
        }

        if (item is long l)
        {
            list.Add(l);
        }
        else if (item is double d)
        {
            list.Add(Convert.ToDecimal(d, CultureInfo.InvariantCulture));
        }
        else if (decimal.TryParse(item.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
        {
            list.Add(dec);
        }
    }

    return list;
}

static List<string> GetStringArray(TomlTable table, string key)
{
    var list = new List<string>();
    if (!table.TryGetValue(key, out var value) || value is not TomlArray arr)
    {
        return list;
    }

    foreach (var item in arr)
    {
        if (item is string s)
        {
            list.Add(s);
        }
        else if (item != null)
        {
            list.Add(item.ToString() ?? string.Empty);
        }
    }

    return list;
}

static string? GitShow(string repoPath, string relativePath)
{
    var psi = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = $"show HEAD:{relativePath}",
        WorkingDirectory = repoPath,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(psi);
    if (process == null)
    {
        return null;
    }

    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        return null;
    }

    return output;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/Railbird.Tools -- phh-to-hrs --input <file|dir> --output <dir> [--max N] [--players 6] [--currency USD]");
    Console.WriteLine("  dotnet run --project src/Railbird.Tools -- phh-to-hrs-git --repo <path> --paths <file> --output <dir> [--max N] [--players 6] [--currency USD]");
}
