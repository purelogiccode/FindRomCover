using FindRomCover.Models;
using MessagePack;

namespace FindRomCover.Managers;

/// <summary>
///     Provides static helpers for loading MAME metadata from the bundled <c>mame.dat</c> file.
/// </summary>
public static class MameManager
{
    private static readonly string DefaultDatPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mame.dat");

    /// <summary>
    ///     Loads all MAME entries from the bundled <c>mame.dat</c> MessagePack file.
    /// </summary>
    /// <returns>The parsed list of <see cref="MameData" /> entries.</returns>
    public static List<MameData> LoadFromDat()
    {
        var datPath = DefaultDatPath;

        if (!File.Exists(datPath))
        {
            // Logged once by the caller (MainWindow.LoadMameDataAsync) — do not log
            // here as well or every missing file files two bug reports.
            throw new MameDatNotFoundException($"The required data file 'mame.dat' was not found at: {datPath}");
        }

        try
        {
            using var stream = File.OpenRead(datPath);

            // MessagePack returns null for a nil/empty payload; keep the non-nullable
            // contract of this method and treat it as an empty catalog.
            return MessagePackSerializer.Deserialize<List<MameData>>(stream) ?? [];
        }
        catch (MessagePackSerializationException ex)
        {
            const string contextMessage = "The mame.dat file is corrupted or in an invalid format.";
            throw new MameDatCorruptError(contextMessage, ex);
        }
        catch (IOException ex)
        {
            const string contextMessage = "Unable to access the mame.dat file (may be in use by another process).";
            throw new IOException(contextMessage, ex);
        }
        catch (Exception ex)
        {
            const string contextMessage = "An unexpected error occurred while loading the mame.dat file.";
            throw new InvalidOperationException(contextMessage, ex);
        }
    }
}