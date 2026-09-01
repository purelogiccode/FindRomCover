using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Xml.Linq;
using FindRomCover.Models;
using FindRomCover.Services;
using MessageBox = System.Windows.MessageBox;

namespace FindRomCover.Managers;

public class SettingsManager : INotifyPropertyChanged
{
    private static SettingsManager? _currentInstance;
    private static readonly Lock InstanceLock = new();

    private static readonly string SettingsFilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.SettingsFileName);

    private static readonly string UserDataSettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FindRomCover", AppConstants.SettingsFileName);

    private static readonly byte[] EncryptionKey = DeriveEncryptionKey();

    // --- Theme settings ---

    private static readonly HashSet<string> ValidBaseThemes = new(StringComparer.OrdinalIgnoreCase) { "Light", "Dark" };

    private static readonly HashSet<string> ValidAccentColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "Red", "Green", "Blue", "Orange", "Purple", "Pink", "Lime", "Emerald",
        "Teal", "Cyan", "Cobalt", "Indigo", "Violet", "Magenta", "Crimson",
        "Amber", "Yellow", "Brown", "Olive", "Steel", "Mauve", "Taupe", "Sienna"
    };

    private readonly Lock _ioLock = new();

    private string _accentColor = "Blue";

    private int _apiTimeoutSeconds = 30;

    private string _baseTheme = "Dark";

    private string _bugReportApiKey = AppConstants.BugReportApiKey;

    private string _bugReportApiUrl = AppConstants.BugReportApiUrl;

    private string _googleKey = string.Empty;

    private int _imageHeight = 300;

    private int _imageLoaderMaxRetries = 3;

    private int _imageLoaderRetryDelayMilliseconds = 200;

    // --- Thumbnail settings (merged: ImageWidth/ImageHeight from FindRomCover, ThumbnailSize from FindRomCover) ---

    private int _imageWidth = 300;

    private string _lastImageFolder = string.Empty;

    private int _maxImagesToLoad = 30;

    // --- FindRomCover-specific settings ---

    private string _searchEngine = "BingWeb";

    private string _selectedSimilarityAlgorithm = string.Empty;

    // --- Similarity settings (from FindRomCover) ---

    private double _similarityThreshold;

    // --- Extensions ---

    private List<string> _supportedExtensions = [];

    // --- MAME settings ---

    private bool _useMameDescriptions;

    // --- Constructor and Load/Save ---

    public SettingsManager()
    {
        lock (InstanceLock)
        {
            if (_currentInstance != null)
            {
                LoadSettings();
                return;
            }

            _currentInstance = this;
        }

        LoadSettings();
    }

    public static SettingsManager? CurrentInstance
    {
        get
        {
            lock (InstanceLock)
            {
                return _currentInstance;
            }
        }
        // ReSharper disable once UnusedMember.Local
        private set
        {
            lock (InstanceLock)
            {
                _currentInstance = value;
            }
        }
    }

    public double SimilarityThreshold
    {
        get => _similarityThreshold;
        set
        {
            value = Math.Clamp(value, 0, 100);
            if (Math.Abs(_similarityThreshold - value) < 0.01) return;

            _similarityThreshold = value;
            OnPropertyChanged(nameof(SimilarityThreshold));
        }
    }

    public string SelectedSimilarityAlgorithm
    {
        get => _selectedSimilarityAlgorithm;
        set
        {
            if (string.Equals(_selectedSimilarityAlgorithm, value, StringComparison.OrdinalIgnoreCase)) return;

            _selectedSimilarityAlgorithm = value;
            OnPropertyChanged(nameof(SelectedSimilarityAlgorithm));
        }
    }

    public int MaxImagesToLoad
    {
        get => _maxImagesToLoad;
        set
        {
            value = Math.Clamp(value, 1, 1000);
            if (_maxImagesToLoad == value) return;

            _maxImagesToLoad = value;
            OnPropertyChanged(nameof(MaxImagesToLoad));
        }
    }

    public int ImageLoaderMaxRetries
    {
        get => _imageLoaderMaxRetries;
        set
        {
            value = Math.Clamp(value, 0, 20);
            if (_imageLoaderMaxRetries == value) return;

            _imageLoaderMaxRetries = value;
            OnPropertyChanged(nameof(ImageLoaderMaxRetries));
        }
    }

    public int ImageLoaderRetryDelayMilliseconds
    {
        get => _imageLoaderRetryDelayMilliseconds;
        set
        {
            value = Math.Clamp(value, 0, 10000);
            if (_imageLoaderRetryDelayMilliseconds == value) return;

            _imageLoaderRetryDelayMilliseconds = value;
            OnPropertyChanged(nameof(ImageLoaderRetryDelayMilliseconds));
        }
    }

    public int ApiTimeoutSeconds
    {
        get => _apiTimeoutSeconds;
        set
        {
            value = Math.Clamp(value, 1, 300);
            if (_apiTimeoutSeconds == value) return;

            _apiTimeoutSeconds = value;
            OnPropertyChanged(nameof(ApiTimeoutSeconds));
        }
    }

    public string LastImageFolder
    {
        get => _lastImageFolder;
        set
        {
            if (string.Equals(_lastImageFolder, value, StringComparison.OrdinalIgnoreCase)) return;

            _lastImageFolder = value;
            OnPropertyChanged(nameof(LastImageFolder));
        }
    }

    public int ImageWidth
    {
        get => _imageWidth;
        set
        {
            value = Math.Clamp(value, 50, 2000);
            if (_imageWidth == value) return;

            _imageWidth = value;
            OnPropertyChanged(nameof(ImageWidth));
        }
    }

    public int ImageHeight
    {
        get => _imageHeight;
        set
        {
            value = Math.Clamp(value, 50, 2000);
            if (_imageHeight == value) return;

            _imageHeight = value;
            OnPropertyChanged(nameof(ImageHeight));
        }
    }

    public int ThumbnailSize
    {
        get => Math.Max(_imageWidth, _imageHeight);
        set
        {
            ImageWidth = value;
            ImageHeight = value;
        }
    }

    public string BaseTheme
    {
        get => _baseTheme;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !ValidBaseThemes.Contains(value)) value = "Dark";

            if (string.Equals(_baseTheme, value, StringComparison.OrdinalIgnoreCase)) return;

            _baseTheme = value;
            OnPropertyChanged(nameof(BaseTheme));
        }
    }

    public string AccentColor
    {
        get => _accentColor;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !ValidAccentColors.Contains(value)) value = "Blue";

            if (string.Equals(_accentColor, value, StringComparison.OrdinalIgnoreCase)) return;

            _accentColor = value;
            OnPropertyChanged(nameof(AccentColor));
        }
    }

    public bool UseMameDescriptions
    {
        get => _useMameDescriptions;
        set
        {
            if (_useMameDescriptions == value) return;

            _useMameDescriptions = value;
            OnPropertyChanged(nameof(UseMameDescriptions));
        }
    }

    public List<string> SupportedExtensions
    {
        get => _supportedExtensions;
        set
        {
            if (_supportedExtensions.SequenceEqual(value, StringComparer.OrdinalIgnoreCase)) return;

            _supportedExtensions = value;
            OnPropertyChanged(nameof(SupportedExtensions));
        }
    }

    public string SearchEngine
    {
        get => _searchEngine;
        set
        {
            if (string.Equals(_searchEngine, value, StringComparison.OrdinalIgnoreCase)) return;

            _searchEngine = value;
            OnPropertyChanged(nameof(SearchEngine));
        }
    }

    public string BugReportApiKey
    {
        get => _bugReportApiKey;
        set
        {
            if (string.Equals(_bugReportApiKey, value, StringComparison.OrdinalIgnoreCase)) return;

            _bugReportApiKey = value;
            OnPropertyChanged(nameof(BugReportApiKey));
        }
    }

    public string BugReportApiUrl
    {
        get => _bugReportApiUrl;
        set
        {
            if (string.Equals(_bugReportApiUrl, value, StringComparison.OrdinalIgnoreCase)) return;

            _bugReportApiUrl = value;
            OnPropertyChanged(nameof(BugReportApiUrl));
        }
    }

    public string GoogleKey
    {
        get => _googleKey;
        set
        {
            if (string.Equals(_googleKey, value, StringComparison.OrdinalIgnoreCase)) return;

            _googleKey = value;
            OnPropertyChanged(nameof(GoogleKey));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static byte[] DeriveEncryptionKey()
    {
        var machineName = Environment.MachineName;
        const string appName = "FindRomCover";
        var salt = Encoding.UTF8.GetBytes($"{machineName}_{appName}_settings");
        const string password = "G4m3C0v3rScr4p3r_S3tt1ngs_K3y_2024";

        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                10000,
                HashAlgorithmName.SHA256,
                32);
        }
        catch (Exception ex)
        {
            // The OS BCrypt/CNG PBKDF2 implementation can fail on some systems
            // (e.g. CryptographicException 0xc1000008), which previously crashed
            // the type initializer and prevented the app from starting at all.
            // Fall back to a fully managed PBKDF2-HMAC-SHA256 implementation,
            // which produces the exact same key (RFC 2898).
            try
            {
                LogService.Warning(ex, "OS PBKDF2 failed; falling back to managed PBKDF2-HMAC-SHA256.");
            }
            catch
            {
                // Logging must never break key derivation.
            }

            return ManagedPbkdf2HmacSha256(Encoding.UTF8.GetBytes(password), salt, 10000, 32);
        }
    }

    private static byte[] ManagedPbkdf2HmacSha256(byte[] password, byte[] salt, int iterations, int outputLength)
    {
        using var hmac = new HMACSHA256(password);
        var hashLength = hmac.HashSize / 8;
        if (hashLength <= 0) throw new CryptographicException("Invalid HMAC hash size.");

        var blockCount = (outputLength + hashLength - 1) / hashLength;
        var derivedKey = new byte[blockCount * hashLength];

        var saltAndBlockIndex = new byte[salt.Length + 4];
        Buffer.BlockCopy(salt, 0, saltAndBlockIndex, 0, salt.Length);

        for (var block = 1; block <= blockCount; block++)
        {
            saltAndBlockIndex[salt.Length] = (byte)(block >> 24);
            saltAndBlockIndex[salt.Length + 1] = (byte)(block >> 16);
            saltAndBlockIndex[salt.Length + 2] = (byte)(block >> 8);
            saltAndBlockIndex[salt.Length + 3] = (byte)block;

            var u = hmac.ComputeHash(saltAndBlockIndex);
            var t = new byte[hashLength];
            Buffer.BlockCopy(u, 0, t, 0, hashLength);

            for (var iteration = 1; iteration < iterations; iteration++)
            {
                u = hmac.ComputeHash(u);
                for (var i = 0; i < hashLength; i++) t[i] ^= u[i];
            }

            Buffer.BlockCopy(t, 0, derivedKey, (block - 1) * hashLength, hashLength);
        }

        if (outputLength == derivedKey.Length) return derivedKey;

        var result = new byte[outputLength];
        Buffer.BlockCopy(derivedKey, 0, result, 0, outputLength);
        return result;
    }

    public void LoadSettings()
    {
        lock (_ioLock)
        {
            try
            {
                var bestPath = GetMostRecentSettingsFilePath();

                if (bestPath == null || !File.Exists(bestPath))
                {
                    // Try to migrate from legacy settings.xml
                    if (TryMigrateFromLegacyXml()) return;

                    SetDefaultSettings();
                    try
                    {
                        SaveSettingsInternal();
                    }
                    catch (Exception saveEx)
                    {
                        LogService.Warning(saveEx, "Failed to save default settings to settings.dat");
                    }

                    return;
                }

                var data = LoadAndDecryptSettings(bestPath);

                if (data == null)
                {
                    // Try to migrate from legacy settings.xml before giving up
                    if (TryMigrateFromLegacyXml()) return;

                    throw new InvalidDataException("Failed to deserialize settings data.");
                }

                SimilarityThreshold = data.SimilarityThreshold;
                SelectedSimilarityAlgorithm = data.SimilarityAlgorithm;
                BaseTheme = data.BaseTheme;
                AccentColor = data.AccentColor;
                ImageWidth = data.ImageWidth;
                ImageHeight = data.ImageHeight;
                MaxImagesToLoad = data.MaxImagesToLoad;
                ImageLoaderMaxRetries = data.ImageLoaderMaxRetries;
                ImageLoaderRetryDelayMilliseconds = data.ImageLoaderRetryDelayMilliseconds;
                ApiTimeoutSeconds = data.ApiTimeoutSeconds;
                SearchEngine = data.SearchEngine;
                BugReportApiKey = data.BugReportApiKey;
                BugReportApiUrl = data.BugReportApiUrl;
                GoogleKey = data.GoogleKey;
                UseMameDescriptions = data.UseMameDescriptions;
                LastImageFolder = data.LastImageFolder;

                if (data.SupportedExtensions.Count > 0)
                    SupportedExtensions = data.SupportedExtensions;
                else
                    SupportedExtensions = GetDefaultExtensions();
            }
            catch (Exception ex)
            {
                // Corrupt or unreadable settings are an environment issue, not a code bug:
                // quarantine the file for diagnostics, warn (instead of reporting a bug) and
                // recover by resetting to default settings.
                LogService.Warning(ex, "Error loading settings from settings.dat; resetting to defaults.");
                QuarantineSettingsFile();

                SetDefaultSettings();
                try
                {
                    SaveSettingsInternal();
                }
                catch (Exception saveEx)
                {
                    LogService.Warning(saveEx, "Failed to save default settings after load error");
                }
            }
        }
    }

    private static SettingsData? LoadAndDecryptSettings(string filePath)
    {
        try
        {
            var encryptedBytes = File.ReadAllBytes(filePath);
            var decryptedBytes = Decrypt(encryptedBytes);
            var json = Encoding.UTF8.GetString(decryptedBytes);

            return JsonSerializer.Deserialize<SettingsData>(json);
        }
        catch (Exception ex)
        {
            // A corrupt or unreadable settings file is an environment issue (e.g. truncated
            // download, drive sync conflict or a machine rename invalidating the key),
            // not an application bug. Warn locally and let LoadSettings reset to defaults.
            LogService.Warning(ex, $"Failed to decrypt settings from: {filePath}");
            return null;
        }
    }

    private bool TryMigrateFromLegacyXml()
    {
        var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");
        if (!File.Exists(legacyPath))
            return false;

        try
        {
            var doc = XDocument.Load(legacyPath);
            var root = doc.Element("Settings");
            if (root == null) return false;

            var legacyData = new SettingsData
            {
                SimilarityThreshold = double.TryParse(
                    root.Element("ThumbnailSize")?.Value,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsedThreshold)
                    ? parsedThreshold
                    : 300,
                SimilarityAlgorithm = AppConstants.Algorithms.JaroWinkler,
                BaseTheme = root.Element("BaseTheme")?.Value ?? "Light",
                AccentColor = root.Element("AccentColor")?.Value ?? "Blue",
                ImageWidth = int.TryParse(root.Element("ThumbnailSize")?.Value, CultureInfo.InvariantCulture, out var w)
                    ? w
                    : 300,
                ImageHeight =
                    int.TryParse(root.Element("ThumbnailSize")?.Value, CultureInfo.InvariantCulture, out var h)
                        ? h
                        : 300,
                MaxImagesToLoad = 30,
                ImageLoaderMaxRetries = 3,
                ImageLoaderRetryDelayMilliseconds = 200,
                ApiTimeoutSeconds = 30,
                SearchEngine = root.Element("SearchEngine")?.Value ?? "BingWeb",
                BugReportApiKey = root.Element("BugReportApiKey")?.Value ?? AppConstants.BugReportApiKey,
                BugReportApiUrl = root.Element("BugReportApiUrl")?.Value ?? AppConstants.BugReportApiUrl,
                GoogleKey = root.Element("GoogleKey")?.Value ?? string.Empty,
                UseMameDescriptions = bool.TryParse(root.Element("UseMameDescriptions")?.Value, out var useMame) &&
                                      useMame,
                LastImageFolder = string.Empty,
                SupportedExtensions = root.Element("SupportedExtensions")?
                    .Elements("Extension")
                    .Select(static x => x.Value)
                    .Where(static x => !string.IsNullOrWhiteSpace(x))
                    .ToList() ?? GetDefaultExtensions()
            };

            // Apply the migrated data
            SimilarityThreshold = legacyData.SimilarityThreshold;
            SelectedSimilarityAlgorithm = legacyData.SimilarityAlgorithm;
            BaseTheme = legacyData.BaseTheme;
            AccentColor = legacyData.AccentColor;
            ImageWidth = legacyData.ImageWidth;
            ImageHeight = legacyData.ImageHeight;
            MaxImagesToLoad = legacyData.MaxImagesToLoad;
            ImageLoaderMaxRetries = legacyData.ImageLoaderMaxRetries;
            ImageLoaderRetryDelayMilliseconds = legacyData.ImageLoaderRetryDelayMilliseconds;
            ApiTimeoutSeconds = legacyData.ApiTimeoutSeconds;
            SearchEngine = legacyData.SearchEngine;
            BugReportApiKey = legacyData.BugReportApiKey;
            BugReportApiUrl = legacyData.BugReportApiUrl;
            GoogleKey = legacyData.GoogleKey;
            UseMameDescriptions = legacyData.UseMameDescriptions;
            LastImageFolder = legacyData.LastImageFolder;
            SupportedExtensions = legacyData.SupportedExtensions.Count > 0
                ? legacyData.SupportedExtensions
                : GetDefaultExtensions();

            // Save in new encrypted format
            SaveSettingsInternal();

            // Rename old file so we don't migrate again
            try
            {
                File.Move(legacyPath, legacyPath + ".migrated", true);
            }
            catch
            {
                // Best effort - not critical
            }

            LogService.Information("Successfully migrated settings from settings.xml to settings.dat");
            return true;
        }
        catch (Exception ex)
        {
            _ = ErrorLogger.LogAsync(ex, "Failed to migrate settings from settings.xml");
            return false;
        }
    }

    private static string? GetMostRecentSettingsFilePath()
    {
        var appDirExists = File.Exists(SettingsFilePath);
        var userDataExists = File.Exists(UserDataSettingsFilePath);

        switch (appDirExists)
        {
            case false when !userDataExists:
                return null;
            case true when !userDataExists:
                return SettingsFilePath;
            case false when userDataExists:
                return UserDataSettingsFilePath;
            default:
                // Both exist - use the most recently modified one
                try
                {
                    var appDirTime = File.GetLastWriteTimeUtc(SettingsFilePath);
                    var userDataTime = File.GetLastWriteTimeUtc(UserDataSettingsFilePath);
                    return userDataTime > appDirTime ? UserDataSettingsFilePath : SettingsFilePath;
                }
                catch
                {
                    // If we can't get the write time, prefer the app directory version
                    return SettingsFilePath;
                }
        }
    }

    private static void QuarantineSettingsFile()
    {
        foreach (var path in new[] { SettingsFilePath, UserDataSettingsFilePath })
        {
            if (!File.Exists(path)) continue;

            try
            {
                var corruptPath = path + ".corrupt";
                File.Move(path, corruptPath, true);
                LogService.Warning($"Quarantined unreadable settings file to: {corruptPath}");
            }
            catch (Exception quarantineEx)
            {
                // Best effort - if we cannot move the file the defaults save below will
                // still overwrite it where permissions allow.
                LogService.Warning(quarantineEx, $"Failed to quarantine unreadable settings file: {path}");
            }
        }
    }

    public void SaveSettings()
    {
        lock (_ioLock)
        {
            SaveSettingsInternal();
        }
    }

    private void SaveSettingsInternal()
    {
        var data = new SettingsData
        {
            SimilarityThreshold = SimilarityThreshold,
            SimilarityAlgorithm = SelectedSimilarityAlgorithm,
            BaseTheme = BaseTheme,
            AccentColor = AccentColor,
            ImageWidth = ImageWidth,
            ImageHeight = ImageHeight,
            MaxImagesToLoad = MaxImagesToLoad,
            ImageLoaderMaxRetries = ImageLoaderMaxRetries,
            ImageLoaderRetryDelayMilliseconds = ImageLoaderRetryDelayMilliseconds,
            ApiTimeoutSeconds = ApiTimeoutSeconds,
            SearchEngine = SearchEngine,
            BugReportApiKey = BugReportApiKey,
            BugReportApiUrl = BugReportApiUrl,
            GoogleKey = GoogleKey,
            UseMameDescriptions = UseMameDescriptions,
            LastImageFolder = LastImageFolder,
            SupportedExtensions = SupportedExtensions
        };

        var json = JsonSerializer.Serialize(data);
        var plaintextBytes = Encoding.UTF8.GetBytes(json);
        var encryptedBytes = Encrypt(plaintextBytes);

        // Try to save to the application directory first
        var savedToAppDir = TrySaveToFile(encryptedBytes, SettingsFilePath);

        // Also save to the user data folder as a backup / fallback
        var savedToUserData = TrySaveToFile(encryptedBytes, UserDataSettingsFilePath);

        switch (savedToAppDir)
        {
            case false when !savedToUserData:
                ShowSaveError("Could not save settings to either location. Your settings changes may be lost.");
                break;
            case false:
                ShowSaveError(
                    "Could not save settings to the application folder. Settings were saved to the user data folder instead.");
                break;
        }
    }

    private static bool TrySaveToFile(byte[] data, string filePath)
    {
        var tempFilePath = filePath + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            File.WriteAllBytes(tempFilePath, data);
            File.Move(tempFilePath, filePath, true);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _ = ErrorLogger.LogAsync(ex, $"Access denied saving settings to: {filePath}");
            return false;
        }
        catch (IOException ex)
        {
            _ = ErrorLogger.LogAsync(ex, $"I/O error saving settings to: {filePath}");
            return false;
        }
        catch (Exception ex)
        {
            _ = ErrorLogger.LogAsync(ex, $"Failed to save settings to: {filePath}");
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
            }
            catch (Exception cleanupEx)
            {
                _ = ErrorLogger.LogAsync(cleanupEx, $"Failed to cleanup settings temp file: {tempFilePath}");
            }
        }
    }

    private static void ShowSaveError(string message)
    {
        if (Application.Current != null)
            MessageBox.Show(message, "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
        else
            _ = ErrorLogger.LogAsync(new InvalidOperationException(message), "Settings save error (no UI available)");
    }

    private void SetDefaultSettings()
    {
        _similarityThreshold =
            double.Parse(AppConstants.Messages.DefaultSimilarityThreshold, CultureInfo.InvariantCulture);
        _selectedSimilarityAlgorithm = AppConstants.Algorithms.JaroWinkler;
        _supportedExtensions = GetDefaultExtensions();
        _imageWidth = 300;
        _imageHeight = 300;
        _baseTheme = AppConstants.Themes.Dark;
        _accentColor = "Blue";
        _useMameDescriptions = false;
        _lastImageFolder = string.Empty;
        _searchEngine = "BingWeb";
        _bugReportApiKey = AppConstants.BugReportApiKey;
        _bugReportApiUrl = AppConstants.BugReportApiUrl;
        _googleKey = string.Empty;
    }

    private static List<string> GetDefaultExtensions()
    {
        return
        [
            "2hd", "3ds", "7z", "88d", "a78", "arc", "bat", "bin", "bs", "cas", "ccd", "cdi", "cdt", "chd", "cht",
            "ciso", "cmd", "col", "cpr", "cso", "cue", "cv", "d64", "d71", "d81", "d88", "dim", "dol", "dsk", "dup",
            "dummy", "elf", "exe", "fdi", "fds", "fig", "g64", "gb", "gcm", "gcz", "gdi", "gg", "gz", "hdf", "hdm",
            "img",
            "int", "ipf", "iso", "lnk", "lnx", "m3u", "mdf", "mds", "ms1", "msa", "mx1", "mx2", "n64", "nbz", "nca",
            "ndd", "nds", "nes", "nib", "nrg", "nro", "nso", "nsp", "o", "pbp", "pce", "prg", "prx", "rar", "ri",
            "rom", "rvz", "sc", "scl", "sda", "sf", "sfc", "sfx", "sg", "smc", "sms", "sna", "st", "stx", "swc",
            "t64", "tap", "tgc", "toc", "trd", "tzx", "u1", "unf", "unif", "url", "v64", "voc", "wad", "wbfs", "wua",
            "xci",
            "xdf", "z64", "z80", "zip", "zso",
            "gba", "gbc", "snes", "smc", "md", "smd", "gen", "32x", "sgg"
        ];
    }

    // --- Encryption/Decryption ---

    private static byte[] Encrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = EncryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();

        // Write IV first
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
            cs.FlushFinalBlock();
        }

        return ms.ToArray();
    }

    private static byte[] Decrypt(byte[] encryptedData)
    {
        if (encryptedData == null || encryptedData.Length < 16)
            throw new ArgumentException("Encrypted data is too short. Expected at least 16 bytes for the IV.",
                nameof(encryptedData));

        using var aes = Aes.Create();
        aes.Key = EncryptionKey;

        // Read IV from the beginning
        var iv = new byte[16];
        Array.Copy(encryptedData, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(encryptedData, 16, encryptedData.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var result = new MemoryStream();

        cs.CopyTo(result);
        return result.ToArray();
    }
}