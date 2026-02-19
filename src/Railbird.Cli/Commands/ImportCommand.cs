using System.CommandLine;
using Railbird.Core.Hrs.Validation;
using Railbird.Storage.Repos;

namespace Railbird.Cli.Commands;

public static class ImportCommand
{
    public static Command Build(HandsRepository repo)
    {
        var pathArg = new Argument<FileInfo>("path", "Path to a hand JSON file")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var cmd = new Command("import", "Import a single hand JSON")
        {
            pathArg
        };

        cmd.SetHandler((FileInfo path) =>
        {
            ImportFromPath(repo, path.FullName);
        }, pathArg);

        return cmd;
    }

    internal static bool ImportFromPath(HandsRepository repo, string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return false;
        }

        var json = File.ReadAllText(path);
        var result = HrsValidator.Validate(json);
        if (!result.IsSuccess)
        {
            Console.Error.WriteLine("Schema validation failed:");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            return false;
        }

        var hand = result.Value!;
        repo.UpsertHand(hand, json);

        Console.WriteLine($"Imported {hand.HandId} | {hand.TimestampUtc} | players: {hand.Players.Count} | events: {hand.Events.Count}");
        return true;
    }
}
