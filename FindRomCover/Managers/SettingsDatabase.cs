using System.Collections.Concurrent;
using FindRomCover.Services;
using Microsoft.Data.Sqlite;

namespace FindRomCover.Managers;

/// <summary>
///     Provides thread-safe read/write access to the key-value settings stored in a SQLite database file.
/// </summary>
public sealed class SettingsDatabase
{
    private static readonly ConcurrentDictionary<string, Lock> FileLocks = new(AppConstants.FilePathComparer);

    private readonly string _filePath;
    private readonly Lock _lock;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SettingsDatabase" /> class for the specified database file.
    /// </summary>
    /// <param name="filePath">The path of the SQLite settings database file.</param>
    public SettingsDatabase(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("A settings database path is required.", nameof(filePath))
            : filePath;
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
            LogService.Debug($"Could not normalize settings database path '{filePath}': {ex.Message}");
            key = filePath;
        }

        return FileLocks.GetOrAdd(key, static _ => new Lock());
    }

    /// <summary>
    ///     Reads all key-value pairs from the settings database.
    /// </summary>
    /// <param name="values">Receives the stored key-value pairs; empty when the load fails.</param>
    /// <param name="isCorrupt">
    ///     Receives <see langword="true" /> when SQLite reported corruption or a non-database
    ///     file, as opposed to a transient lock/permission failure.
    /// </param>
    /// <returns><c>true</c> if the database was read successfully; otherwise <c>false</c>.</returns>
    public bool TryLoadAll(out Dictionary<string, string> values, out bool isCorrupt)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        isCorrupt = false;

        lock (_lock)
        {
            try
            {
                using var connection = OpenConnection(readOnly: false);
                return TryReadAll(connection, values);
            }
            catch (SqliteException ex) when (IsReadOnlyOrCannotOpen(ex))
            {
                // A readable database with a read-only attribute (backup restore, root
                // ownership, sync client) still contains the user's settings. Read it
                // without attempting the CREATE TABLE.
                try
                {
                    using var connection = OpenConnection(readOnly: true);
                    return TryReadAll(connection, values);
                }
                catch (Exception readOnlyEx)
                {
                    LogService.WarningTransient(readOnlyEx,
                        "Settings database could not be read even in read-only mode.");
                    values = new Dictionary<string, string>(StringComparer.Ordinal);
                    return false;
                }
            }
            catch (Exception ex)
            {
                isCorrupt = IsCorruptionException(ex);
                LogService.WarningTransient(ex, "Settings database could not be read.");
                values = new Dictionary<string, string>(StringComparer.Ordinal);
                return false;
            }
        }
    }

    private static bool TryReadAll(SqliteConnection connection, Dictionary<string, string> values)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM Settings";

        using var reader = command.ExecuteReader();
        while (reader.Read())
            values[reader.GetString(0)] = reader.GetString(1);

        return true;
    }

    private static bool IsReadOnlyOrCannotOpen(SqliteException exception)
    {
        // SQLITE_PERM (3), SQLITE_READONLY (8), SQLITE_CANTOPEN (14).
        return exception.SqliteErrorCode is 3 or 8 or 14;
    }

    private static bool IsCorruptionException(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
            if (current is SqliteException { SqliteErrorCode: 11 or 26 })
                return true;

        return false;
    }

    /// <summary>
    ///     Inserts or updates all supplied key-value pairs in the settings database.
    /// </summary>
    /// <param name="values">The key-value pairs to persist.</param>
    /// <returns><c>true</c> if the values were saved successfully; otherwise <c>false</c>.</returns>
    public bool SaveAll(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        lock (_lock)
        {
            try
            {
                using var connection = OpenConnection(readOnly: false);
                using var transaction = connection.BeginTransaction();

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "INSERT INTO Settings (Key, Value) VALUES ($key, $value) " +
                        "ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value";

                    var keyParameter = command.Parameters.Add("$key", SqliteType.Text);
                    var valueParameter = command.Parameters.Add("$value", SqliteType.Text);

                    foreach (var pair in values)
                    {
                        keyParameter.Value = pair.Key;
                        valueParameter.Value = pair.Value;
                        command.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                LogService.WarningTransient(ex, "Settings database could not be saved.");
                return false;
            }
        }
    }

    private SqliteConnection OpenConnection(bool readOnly)
    {
        if (!readOnly)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _filePath,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();

            if (!readOnly)
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    "CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL)";
                command.ExecuteNonQuery();
            }

            return connection;
        }
        catch
        {
            // Never leak the connection when schema creation fails on a read-only or
            // corrupt database file.
            connection.Dispose();
            throw;
        }
    }
}
