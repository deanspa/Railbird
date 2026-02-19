using Microsoft.Data.Sqlite;

namespace Railbird.Storage.Db;

public static class MigrationRunner
{
    public static void EnsureDatabase(SqliteConnectionFactory factory)
    {
        using var connection = factory.Open();
        EnsureMigrated(connection);
    }

    private static void EnsureMigrated(SqliteConnection connection)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='hands';";
        var exists = check.ExecuteScalar() != null;
        if (exists)
        {
            return;
        }

        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Db", "Migrations");
        var scriptPath = Path.Combine(migrationsDir, "001_init.sql");
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Migration script not found at {scriptPath}");
        }

        var sql = File.ReadAllText(scriptPath);
        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
        tx.Commit();
    }
}
