using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FindRomCover.Services;

/// <summary>
///     Cross-platform click sound player:
///     Windows uses MCI (winmm), macOS uses afplay, Linux tries the common
///     command-line players. Playback failures silently disable the sound.
/// </summary>
public sealed class LocalAudioService : IAudioService
{
    private const string MciAlias = "FindRomCoverClick";

    private readonly string _soundPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio", "click.mp3");

    private bool _isSoundAvailable;

    public LocalAudioService()
    {
        _isSoundAvailable = File.Exists(_soundPath);
    }

    public void PlayClickSound()
    {
        if (!_isSoundAvailable) return;

        try
        {
            Play(_soundPath);
        }
        catch
        {
            _isSoundAvailable = false;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static void Play(string path)
    {
        if (OperatingSystem.IsWindows())
            PlayWindows(path);
        else if (OperatingSystem.IsMacOS())
            TryStart("afplay", [path]);
        else
            PlayLinux(path);
    }

    private static void PlayLinux(string path)
    {
        if (TryStart("paplay", [path])) return;
        if (TryStart("ffplay", ["-nodisp", "-autoexit", "-loglevel", "quiet", path])) return;
        if (TryStart("mpg123", ["-q", path])) return;
        TryStart("aplay", [path]);
    }

    private static bool TryStart(string fileName, string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            return process != null;
        }
        catch
        {
            return false;
        }
    }

    private static void PlayWindows(string path)
    {
        _ = mciSendStringW($"close {MciAlias}", null, 0, IntPtr.Zero);

        var openResult = mciSendStringW($"open \"{path}\" type mpegvideo alias {MciAlias}", null, 0, IntPtr.Zero);
        if (openResult == 0)
            _ = mciSendStringW($"play {MciAlias}", null, 0, IntPtr.Zero);
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendStringW(string command, System.Text.StringBuilder? returnString, int returnLength,
        IntPtr hwndCallback);
}
