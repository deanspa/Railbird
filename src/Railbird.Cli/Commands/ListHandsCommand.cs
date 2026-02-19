using System.CommandLine;
using Railbird.Storage.Repos;

namespace Railbird.Cli.Commands;

public static class ListHandsCommand
{
    public static Command Build(HandsRepository repo)
    {
        var takeOption = new Option<int>("--take", () => 50, "Number of hands to list");
        var cmd = new Command("list", "List recent hands")
        {
            takeOption
        };

        cmd.SetHandler((int take) =>
        {
            var items = repo.ListHands(take);
            if (items.Count == 0)
            {
                Console.WriteLine("No hands found.");
                return;
            }

            foreach (var item in items)
            {
                var stakes = $"{item.SmallBlind}/{item.BigBlind} {item.Currency}";
                var hero = item.HeroSeat.HasValue ? $"hero seat {item.HeroSeat.Value}" : "hero seat n/a";
                Console.WriteLine($"{item.TimestampUtc} | {item.HandId} | {stakes} | {hero} | players {item.PlayerCount} | events {item.EventCount}");
            }
        }, takeOption);

        return cmd;
    }
}
