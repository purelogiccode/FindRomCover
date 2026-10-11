using System.Collections.Concurrent;
using FindRomCover.Services;
using Microsoft.Data.Sqlite;

namespace FindRomCover.Managers;

/// <summary>
///     Provides thread-safe read/write access to the key-value settings stored in a SQLite database file.
/// </summary>
public sealed class SettingsDatabase
{
    private static readonly ConcurrentDictionary<string, Lock> FileLocks = new(StringComparer.OrdinalIgnoreCase);

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
    /// <returns><c>true</c> if the database was read successfully; otherwise <c>false</c>.</returns>
    public bool TryLoadAll(out Dictionary<string, string> values)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);

        lock (_lock)
        {
            try
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT Key, Value FROM Settings";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                    values[reader.GetString(0)] = reader.GetString(1);

                return true;
            }
            catch (Exception ex)
            {
                LogService.WarningTransient(ex, "Settings database could not be read.");
                values = new Dictionary<string, string>(StringComparer.Ordinal);
                return false;
            }
        }
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
                using var connection = OpenConnection();
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

            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL)";
            command.ExecuteNonQuery();

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
