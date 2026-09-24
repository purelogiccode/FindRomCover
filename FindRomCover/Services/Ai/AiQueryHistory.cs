using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace FindRomCover.Services.Ai;

public sealed class AiQueryHistory
{
    private const int MaxEntries = 100_000;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(180);
    private static readonly ConcurrentDictionary<string, Lock> FileLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _filePath;
    private readonly Lock _lock;
    private readonly TimeSpan _ttl;

    public AiQueryHistory(string? filePath = null, TimeSpan? ttl = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? DefaultFilePath : filePath;
        _ttl = ttl ?? DefaultTtl;
        _lock = GetFileLock(_filePath);
    }

    private static Lock GetFileLock(string filePath)
    {
        string key;
        try
        {
            key = Path.GetFullPath(filePath);
        }
        catch (Exception)
        {
            key = filePath;
        }

        return FileLocks.GetOrAdd(key, static _ => new Lock());
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FindRomCover",
        AppConstants.QueryHistoryFileName);

    public int Count
    {
        get
        {
            lock (_lock)
            {
                try
                {
                    using var connection = OpenConnection();
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM QueryHistory";
                    return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    LogService.Warning(ex, "AI query history could not be read.");
                    return 0;
                }
            }
        }
    }

    /// <summary>
    ///     Returns the normalized paths of all non-expired entries using a single query.
    ///     Prefer this over calling <see cref="WasQueried"/> per item for large lists.
    /// </summary>
    public HashSet<string> GetQueriedPathSet()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        lock (_lock)
        {
            try
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT TargetPath, QueriedUtc FROM QueryHistory";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var queriedUtc = ParseTimestamp(reader.GetString(1));
                    if (queriedUtc != null && DateTimeOffset.UtcNow - queriedUtc.Value <= _ttl)
                        paths.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                LogService.Warning(ex, "AI query history could not be read.");
            }
        }

        return paths;
    }

    public bool WasQueried(string targetPath)
    {
        var key = NormalizeKey(targetPath);
        if (key.Length == 0) return false;

        lock (_lock)
        {
            try
            {
                using var connection = OpenConnection();
                using var select = connection.CreateCommand();
                select.CommandText = "SELECT QueriedUtc FROM QueryHistory WHERE TargetPath = $path";
                select.Parameters.AddWithValue("$path", key);

                if (select.ExecuteScalar() is not string stored) return false;

                var queriedUtc = ParseTimestamp(stored);
                if (queriedUtc != null && DateTimeOffset.UtcNow - queriedUtc.Value <= _ttl) return true;

                using var delete = connection.CreateCommand();
                delete.CommandText = "DELETE FROM QueryHistory WHERE TargetPath = $path";
                delete.Parameters.AddWithValue("$path", key);
                delete.ExecuteNonQuery();
                return false;
            }
            catch (Exception ex)
            {
                LogService.Warning(ex, "AI query history could not be read.");
                return false;
            }
        }
    }

    public void MarkQueried(string targetPath, string outcome)
    {
        var key = NormalizeKey(targetPath);
        if (key.Length == 0) return;

        lock (_lock)
        {
            try
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText =
                    "INSERT INTO QueryHistory (TargetPath, QueriedUtc, Outcome) VALUES ($path, $queried, $outcome) " +
                    "ON CONFLICT(TargetPath) DO UPDATE SET QueriedUtc = excluded.QueriedUtc, Outcome = excluded.Outcome";
                command.Parameters.AddWithValue("$path", key);
                command.Parameters.AddWithValue("$queried", FormatTimestamp(DateTimeOffset.UtcNow));
                command.Parameters.AddWithValue("$outcome", outcome ?? string.Empty);
                command.ExecuteNonQuery();

                Prune(connection);
            }
            catch (Exception ex)
            {
                LogService.Warning(ex, "AI query history could not be saved.");
            }
        }
    }

    public void Remove(string targetPath)
    {
        var key = NormalizeKey(targetPath);
        if (key.Length == 0) return;

        lock (_lock)
        {
            try
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM QueryHistory WHERE TargetPath = $path";
                command.Parameters.AddWithValue("$path", key);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                LogService.Warning(ex, "AI query history could not be updated.");
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            try
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM QueryHistory";
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                LogService.Warning(ex, "AI query history could not be cleared.");
            }
        }
    }

    internal static string NormalizeKey(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return string.Empty;

        try
        {
            return Path.GetFullPath(targetPath.Trim()).ToLowerInvariant();
        }
        catch (Exception)
        {
            return targetPath.Trim().ToLowerInvariant();
        }
    }

    private void Prune(SqliteConnection connection)
    {
        using (var expire = connection.CreateCommand())
        {
            expire.CommandText = "DELETE FROM QueryHistory WHERE QueriedUtc < $cutoff";
            expire.Parameters.AddWithValue("$cutoff", FormatTimestamp(DateTimeOffset.UtcNow.Subtract(_ttl)));
            expire.ExecuteNonQuery();
        }

        int count;
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM QueryHistory";
            count = Convert.ToInt32(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        if (count <= MaxEntries) return;

        using var trim = connection.CreateCommand();
        trim.CommandText =
            "DELETE FROM QueryHistory WHERE TargetPath IN (" +
            "SELECT TargetPath FROM QueryHistory ORDER BY QueriedUtc ASC LIMIT $excess)";
        trim.Parameters.AddWithValue("$excess", count - MaxEntries);
        trim.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _filePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS QueryHistory (" +
            "TargetPath TEXT PRIMARY KEY COLLATE NOCASE, QueriedUtc TEXT NOT NULL, Outcome TEXT NOT NULL)";
        command.ExecuteNonQuery();

        return connection;
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset? ParseTimestamp(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }
}
