using System.IO;
using FindRomCover.Services;
using Microsoft.Data.Sqlite;

namespace FindRomCover.Managers;

public sealed class SettingsDatabase
{
    private readonly string _filePath;
    private readonly Lock _lock = new();

    public SettingsDatabase(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("A settings database path is required.", nameof(filePath))
            : filePath;
    }

    public Dictionary<string, string> LoadAll()
    {
        lock (_lock)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            try
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT Key, Value FROM Settings";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                    values[reader.GetString(0)] = reader.GetString(1);
            }
            catch (Exception ex)
            {
                LogService.Warning(ex, "Settings database could not be read.");
            }

            return values;
        }
    }

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
                LogService.Warning(ex, "Settings database could not be saved.");
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
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL)";
        command.ExecuteNonQuery();

        return connection;
    }
}
