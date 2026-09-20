using System.IO;
using System.Text.Json;

namespace FindRomCover.Services.Ai;

public sealed class AiVerdictCache
{
    private const int MaxEntries = 2000;
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(30);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _filePath;
    private readonly Lock _lock = new();
    private Dictionary<string, CacheEntry>? _entries;

    public AiVerdictCache(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? DefaultFilePath : filePath;
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FindRomCover",
        "ai-cache.json");

    public bool TryGet<T>(string key, out T? value) where T : class
    {
        value = null;

        lock (_lock)
        {
            var entries = EnsureLoaded();
            if (!entries.TryGetValue(key, out var entry)) return false;

            if (DateTimeOffset.UtcNow - entry.CreatedUtc > Ttl)
            {
                entries.Remove(key);
                return false;
            }

            try
            {
                value = JsonSerializer.Deserialize<T>(entry.Payload, JsonOptions);
                return value != null;
            }
            catch (JsonException)
            {
                entries.Remove(key);
                return false;
            }
        }
    }

    public void Set<T>(string key, T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);

        lock (_lock)
        {
            var entries = EnsureLoaded();
            entries[key] = new CacheEntry
            {
                CreatedUtc = DateTimeOffset.UtcNow,
                Payload = JsonSerializer.Serialize(value, JsonOptions)
            };

            Prune(entries);
            SaveCore(entries);
        }
    }

    private Dictionary<string, CacheEntry> EnsureLoaded()
    {
        return _entries ??= LoadCore();
    }

    private Dictionary<string, CacheEntry> LoadCore()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new Dictionary<string, CacheEntry>(StringComparer.Ordinal);

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(json, JsonOptions)
                   ?? new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "AI verdict cache could not be read; starting with an empty cache.");
            return new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        }
    }

    private static void Prune(Dictionary<string, CacheEntry> entries)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in entries.Where(kvp => now - kvp.Value.CreatedUtc > Ttl).Select(static kvp => kvp.Key).ToList())
            entries.Remove(key);

        if (entries.Count <= MaxEntries) return;

        foreach (var key in entries.OrderBy(static kvp => kvp.Value.CreatedUtc)
                     .Take(entries.Count - MaxEntries)
                     .Select(static kvp => kvp.Key)
                     .ToList())
            entries.Remove(key);
    }

    private void SaveCore(Dictionary<string, CacheEntry> entries)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_filePath, JsonSerializer.Serialize(entries, JsonOptions));
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "AI verdict cache could not be saved.");
        }
    }

    private sealed class CacheEntry
    {
        public DateTimeOffset CreatedUtc { get; set; }
        public string Payload { get; set; } = string.Empty;
    }
}
