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
                "process TEXT," +
                "file TEXT," +
                "action TEXT," +
                "result TEXT," +
                "risk TEXT," +
                "severity TEXT NOT NULL," +
                "source TEXT NOT NULL," +
                "summary TEXT NOT NULL," +
                "evidence_json TEXT NOT NULL DEFAULT '[]'," +
                "details_json TEXT NOT NULL" +
                ");" +
                "CREATE INDEX IF NOT EXISTS ix_security_events_created " +
                "ON security_events(created_utc DESC);" +
                "CREATE INDEX IF NOT EXISTS ix_security_events_type " +
                "ON security_events(type);" +
                "CREATE TABLE IF NOT EXISTS quarantine_items (" +
                "id TEXT PRIMARY KEY," +
                "created_utc TEXT NOT NULL," +
                "original_path TEXT NOT NULL," +
                "sha256 TEXT," +
                "size_bytes INTEGER," +
                "status TEXT NOT NULL" +
                ");";

            await command.ExecuteNonQueryAsync();

            await EnsureColumnAsync(connection, "security_events", "process", "TEXT");
            await EnsureColumnAsync(connection, "security_events", "file", "TEXT");
            await EnsureColumnAsync(connection, "security_events", "action", "TEXT");
            await EnsureColumnAsync(connection, "security_events", "result", "TEXT");
            await EnsureColumnAsync(connection, "security_events", "risk", "TEXT");
            await EnsureColumnAsync(connection, "security_events", "evidence_json", "TEXT NOT NULL DEFAULT '[]'");
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        string definition)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";

        await using var reader = await check.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await alter.ExecuteNonQueryAsync();
    }

    public async Task RecordAsync(
        string type,
        string severity,
        string source,
        string summary,
        object details,
        string? process = null,
        string? file = null,
        string? action = null,
        string? result = null,
        string? risk = null,
        IEnumerable<string>? evidence = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var evidenceJson = JsonSerializer.Serialize(
            evidence?.Where(x => !string.IsNullOrWhiteSpace(x)).Take(32)
            ?? Enumerable.Empty<string>());

        await gate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={paths.DatabasePath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO security_events " +
                "(id, created_utc, type, process, file, action, result, risk, severity, source, summary, evidence_json, details_json) " +
                "VALUES ($id, $created, $type, $process, $file, $action, $result, $risk, $severity, $source, $summary, $evidence, $details);";

            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$type", type);
            command.Parameters.AddWithValue("$process", (object?)process ?? DBNull.Value);
            command.Parameters.AddWithValue("$file", (object?)file ?? DBNull.Value);
            command.Parameters.AddWithValue("$action", (object?)action ?? DBNull.Value);
            command.Parameters.AddWithValue("$result", (object?)result ?? DBNull.Value);
            command.Parameters.AddWithValue("$risk", (object?)risk ?? DBNull.Value);
            command.Parameters.AddWithValue("$severity", severity);
            command.Parameters.AddWithValue("$source", source);
            command.Parameters.AddWithValue("$summary", summary);
            command.Parameters.AddWithValue("$evidence", evidenceJson);
            command.Parameters.AddWithValue("$details", JsonSerializer.Serialize(details));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<IReadOnlyList<Dictionary<string, object?>>> RecentAsync(int limit = 100) =>
        QueryAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, limit, null, null);

    public Task<IReadOnlyList<Dictionary<string, object?>>> TodayAsync(int limit = 200)
    {
        var now = DateTimeOffset.Now;
        var start = new DateTimeOffset(now.Date, now.Offset).ToUniversalTime();
        var end = start.AddDays(1);
        return QueryAsync(start, end, limit, null, null);
    }

    public Task<IReadOnlyList<Dictionary<string, object?>>> SearchAsync(
        string? type = null,
        string? risk = null,
        int limit = 200)
    {
        return QueryAsync(
            DateTimeOffset.MinValue,
            DateTimeOffset.MaxValue,
            limit,
            string.IsNullOrWhiteSpace(type) ? null : type,
            string.IsNullOrWhiteSpace(risk) ? null : risk);
    }

    private async Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int limit,
        string? type,
        string? risk)
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
                "SELECT id, created_utc, type, process, file, action, result, risk, severity, source, summary, evidence_json, details_json " +
                "FROM security_events " +
                "WHERE created_utc >= $start AND created_utc < $end " +
                "AND ($type IS NULL OR type = $type) " +
                "AND ($risk IS NULL OR risk = $risk) " +
                "ORDER BY created_utc DESC LIMIT " + limit + ";";

            command.Parameters.AddWithValue("$start", startUtc == DateTimeOffset.MinValue
                ? DateTimeOffset.MinValue.ToString("O")
                : startUtc.ToString("O"));
            command.Parameters.AddWithValue("$end", endUtc == DateTimeOffset.MaxValue
                ? DateTimeOffset.MaxValue.ToString("O")
                : endUtc.ToString("O"));
            command.Parameters.AddWithValue("$type", (object?)type ?? DBNull.Value);
            command.Parameters.AddWithValue("$risk", (object?)risk ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["id"] = reader.GetString(0),
                    ["createdUtc"] = reader.GetString(1),
                    ["type"] = reader.GetString(2),
                    ["process"] = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ["file"] = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ["action"] = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ["result"] = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ["risk"] = reader.IsDBNull(7) ? null : reader.GetString(7),
                    ["severity"] = reader.GetString(8),
                    ["source"] = reader.GetString(9),
                    ["summary"] = reader.GetString(10),
                    ["evidence"] = reader.GetString(11),
                    ["details"] = reader.GetString(12)
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
        string id,
        string originalPath,
        string? sha256,
        long? sizeBytes,
        string status)
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
