using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace FindRomCover.Services.Ai;

/// <summary>
///     Tracks which target cover files have already been queried so batch runs can skip them.
///     Entries are stored in a SQLite database and expire after a configurable time-to-live.
/// </summary>
public sealed class AiQueryHistory
{
    private const int MaxEntries = 100_000;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(180);
    private static readonly ConcurrentDictionary<string, Lock> FileLocks = new(AppConstants.FilePathComparer);

    private readonly string _filePath;
    private readonly Lock _lock;
    private readonly TimeSpan _ttl;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiQueryHistory"/> class.
    /// </summary>
    /// <param name="filePath">The database file path; defaults to <see cref="DefaultFilePath"/>.</param>
    /// <param name="ttl">How long entries remain valid; defaults to 180 days.</param>
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
        catch (Exception ex)
        {
            LogService.Debug($"Could not normalize query history path '{filePath}': {ex.Message}");
            key = filePath;
        }

        return FileLocks.GetOrAdd(key, static _ => new Lock());
    }

    /// <summary>
    ///     Gets the default query history database path under the local application data folder.
    /// </summary>
    private static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FindRomCover",
        AppConstants.QueryHistoryFileName);

    /// <summary>
    ///     Gets the total number of stored entries, or 0 when the database cannot be read.
    /// </summary>
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
                    LogService.WarningTransient(ex, "AI query history could not be read.");
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
        var paths = new HashSet<string>(AppConstants.FilePathComparer);

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
                LogService.WarningTransient(ex, "AI query history could not be read.");
            }
        }

        return paths;
    }

    /// <summary>
    ///     Determines whether the target path has a non-expired entry, deleting the entry when it has expired.
    /// </summary>
    /// <param name="targetPath">The target cover file path to check.</param>
    /// <returns><see langword="true"/> when the path was queried within the time-to-live; otherwise <see langword="false"/>.</returns>
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
                LogService.WarningTransient(ex, "AI query history could not be read.");
                return false;
            }
        }
    }

    /// <summary>
    ///     Records or refreshes the query entry for the target path and prunes expired or excess rows.
    /// </summary>
    /// <param name="targetPath">The target cover file path to record.</param>
    /// <param name="outcome">A short outcome label stored with the entry.</param>
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
                command.Parameters.AddWithValue("$outcome", outcome);
                command.ExecuteNonQuery();

                Prune(connection);
            }
            catch (Exception ex)
            {
                LogService.WarningTransient(ex, "AI query history could not be saved.");
            }
        }
    }

    /// <summary>
    ///     Deletes the entry for the target path, if present.
    /// </summary>
    /// <param name="targetPath">The target cover file path whose entry should be removed.</param>
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
                LogService.WarningTransient(ex, "AI query history could not be updated.");
            }
        }
    }

    /// <summary>
    ///     Deletes all entries from the query history.
    /// </summary>
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
                LogService.WarningTransient(ex, "AI query history could not be cleared.");
            }
        }
    }

    /// <summary>
    ///     Normalizes a target path to a full, lower-case key suitable for database lookups.
    /// </summary>
    /// <param name="targetPath">The path to normalize.</param>
    /// <returns>The normalized key, or an empty string when the path is blank.</returns>
    internal static string NormalizeKey(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return string.Empty;

        try
        {
            var fullPath = Path.GetFullPath(targetPath.Trim());
            // Windows paths are case-insensitive; Linux and macOS (case-sensitive
            // volumes) can legitimately contain both "Sonic.png" and "sonic.png".
            return OperatingSystem.IsWindows() ? fullPath.ToLowerInvariant() : fullPath;
        }
        catch (Exception ex)
        {
            LogService.Debug($"Could not normalize key '{targetPath}': {ex.Message}");
            return OperatingSystem.IsWindows() ? targetPath.Trim().ToLowerInvariant() : targetPath.Trim();
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
        try
        {
            connection.Open();
            CreateSchema(connection);
            return connection;
        }
        catch (SqliteException ex) when (IsCorruptionError(ex))
        {
            // The file opened but is not a valid SQLite database (SQLite reports
            // "file is not a database" lazily on first access): quarantine it and
            // start a fresh database. Any other failure (read-only file, disk full,
            // locked database, sharing violation) must NOT move the healthy file
            // aside; those propagate and callers log them as transient.
            connection.Dispose();
            LogService.WarningTransient(ex,
                $"AI query history file '{_filePath}' is corrupt; quarantining it and recreating the database.");
            QuarantineCorruptFile();

            return CreateFreshDatabase(connectionString);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static bool IsCorruptionError(SqliteException ex)
    {
        // SQLITE_CORRUPT (11): the database disk image is malformed.
        // SQLITE_NOTADB (26): the file is not a database.
        return ex.SqliteErrorCode is 11 or 26;
    }

    private static SqliteConnection CreateFreshDatabase(string connectionString)
    {
        var recreated = new SqliteConnection(connectionString);
        try
        {
            recreated.Open();
            CreateSchema(recreated);
            return recreated;
        }
        catch
        {
            recreated.Dispose();
            throw;
        }
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS QueryHistory (" +
                "TargetPath TEXT PRIMARY KEY COLLATE BINARY, QueriedUtc TEXT NOT NULL, Outcome TEXT NOT NULL)";
            command.ExecuteNonQuery();
        }

        // Older versions used COLLATE NOCASE, which merges paths that differ only in
        // case on Linux/macOS. Rebuild the table once so the primary key is binary.
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'QueryHistory'";
        if (check.ExecuteScalar() is not string schema ||
            !schema.Contains("NOCASE", StringComparison.OrdinalIgnoreCase))
            return;

        using var migrate = connection.CreateCommand();
        migrate.CommandText =
            "ALTER TABLE QueryHistory RENAME TO QueryHistory_old;" +
            "CREATE TABLE QueryHistory (" +
            "TargetPath TEXT PRIMARY KEY COLLATE BINARY, QueriedUtc TEXT NOT NULL, Outcome TEXT NOT NULL);" +
            "INSERT INTO QueryHistory (TargetPath, QueriedUtc, Outcome) " +
            "SELECT TargetPath, QueriedUtc, Outcome FROM QueryHistory_old;" +
            "DROP TABLE QueryHistory_old;";
        migrate.ExecuteNonQuery();
    }

    private void QuarantineCorruptFile()
    {
        try
        {
            if (!File.Exists(_filePath)) return;

            File.Move(_filePath, _filePath + ".corrupt", true);
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, $"Could not quarantine the corrupt AI query history file '{_filePath}'.");
        }
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset? ParseTimestamp(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }
}
