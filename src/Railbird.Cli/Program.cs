using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Railbird.Cli.Commands;
using Railbird.Storage.Db;
using Railbird.Storage.Repos;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = config.GetConnectionString("RailbirdDb") ?? ".local/railbird.db";

var factory = new SqliteConnectionFactory(connectionString);
MigrationRunner.EnsureDatabase(factory);

var repo = new HandsRepository(factory);

var root = new RootCommand("Railbird CLI");
root.AddCommand(ImportCommand.Build(repo));
root.AddCommand(ListHandsCommand.Build(repo));
root.AddCommand(ImportExamplesCommand.Build(repo));

return await root.InvokeAsync(args);
