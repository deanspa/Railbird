using System.CommandLine;
using Railbird.Cli;
using Railbird.Storage.Repos;

namespace Railbird.Cli.Commands;

public static class ImportExamplesCommand
{
    public static Command Build(HandsRepository repo)
    {
        var cmd = new Command("import-examples", "Import all example hands")
        {
        };

        cmd.SetHandler(() =>
        {
            var root = RepoLocator.FindRepoRoot();
            var examplesDir = Path.Combine(root, "examples", "hands", "v1");
            if (!Directory.Exists(examplesDir))
            {
                Console.Error.WriteLine($"Examples directory not found: {examplesDir}");
                return;
            }

            var files = Directory.GetFiles(examplesDir, "*.json");
            if (files.Length == 0)
            {
                Console.WriteLine("No example hands found.");
                return;
            }

            var success = 0;
            foreach (var file in files)
            {
                if (ImportCommand.ImportFromPath(repo, file))
                {
                    success++;
                }
            }

            Console.WriteLine($"Imported {success}/{files.Length} example hands.");
        });

        return cmd;
    }
}
