using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Maverick.Core;

public sealed class Journal
{
    private readonly CorePaths paths;
    private readonly SemaphoreSlim gate = new(1, 1);

    public Journal(CorePaths paths)
    {
        this.paths = paths;
        InitializeAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeAsync()
    {
        await gate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={paths.DatabasePath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                "PRAGMA journal_mode=WAL;" +
                "CREATE TABLE IF NOT EXISTS security_events (" +
                "id TEXT PRIMARY KEY," +
                "created_utc TEXT NOT NULL," +
                "type TEXT NOT NULL," +
                "severity TEXT NOT NULL," +
                "source TEXT NOT NULL," +
                "summary TEXT NOT NULL," +
                "details_json TEXT NOT NULL" +
                ");" +
                "CREATE INDEX IF NOT EXISTS ix_security_events_created " +
                "ON security_events(created_utc DESC);" +
                "CREATE TABLE IF NOT EXISTS quarantine_items (" +
                "id TEXT PRIMARY KEY," +
                "created_utc TEXT NOT NULL," +
                "original_path TEXT NOT NULL," +
                "sha256 TEXT," +
                "size_bytes INTEGER," +
                "status TEXT NOT NULL" +
                ");";

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RecordAsync(
        string type, string severity, string source,
        string summary, object details)
    {
        var id = Guid.NewGuid().ToString("N");

        await gate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={paths.DatabasePath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO security_events " +
                "(id, created_utc, type, severity, source, summary, details_json) " +
                "VALUES ($id, $created, $type, $severity, $source, $summary, $details);";

            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$type", type);
            command.Parameters.AddWithValue("$severity", severity);
            command.Parameters.AddWithValue("$source", source);
            command.Parameters.AddWithValue("$summary", summary);
            command.Parameters.AddWithValue("$details", JsonSerializer.Serialize(details));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> RecentAsync(int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 500);
        var rows = new List<Dictionary<string, object?>>();

        await gate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={paths.DatabasePath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT id, created_utc, type, severity, source, summary, details_json " +
                "FROM security_events ORDER BY created_utc DESC LIMIT " + limit + ";";

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["id"] = reader.GetString(0),
                    ["createdUtc"] = reader.GetString(1),
                    ["type"] = reader.GetString(2),
                    ["severity"] = reader.GetString(3),
                    ["source"] = reader.GetString(4),
                    ["summary"] = reader.GetString(5),
                    ["details"] = reader.GetString(6)
                });
            }
        }
        finally
        {
            gate.Release();
        }

        return rows;
    }

    public async Task RecordQuarantineAsync(
        string id, string originalPath, string? sha256,
        long? sizeBytes, string status)
    {
        await gate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={paths.DatabasePath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT OR REPLACE INTO quarantine_items " +
                "(id, created_utc, original_path, sha256, size_bytes, status) " +
                "VALUES ($id, $created, $path, $hash, $size, $status);";

            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$path", originalPath);
            command.Parameters.AddWithValue("$hash", (object?)sha256 ?? DBNull.Value);
            command.Parameters.AddWithValue("$size", (object?)sizeBytes ?? DBNull.Value);
            command.Parameters.AddWithValue("$status", status);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            gate.Release();
        }
    }
}
