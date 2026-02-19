using Microsoft.Data.Sqlite;

namespace Railbird.Storage.Db;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string dbPathOrConnectionString)
    {
        _connectionString = NormalizeConnectionString(dbPathOrConnectionString);
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    private static string NormalizeConnectionString(string input)
    {
        if (input.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }

        var fullPath = Path.GetFullPath(input);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return $"Data Source={fullPath}";
    }
}
