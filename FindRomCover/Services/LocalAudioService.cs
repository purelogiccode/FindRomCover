using System.Diagnostics;
using System.Runtime.InteropServices;
using FindRomCover.Interfaces;

namespace FindRomCover.Services;

/// <summary>
///     Cross-platform click sound player:
///     Windows uses MCI (winmm), macOS uses afplay, Linux tries the common
///     command-line players. Playback failures silently disable the sound.
/// </summary>
public sealed class LocalAudioService : IAudioService
{
    private const string MciAlias = "FindRomCoverClick";

    private readonly Lock _playbackLock = new();
    private readonly string _soundPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio", "click.mp3");

    private bool _disposed;
    private bool _isSoundAvailable;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LocalAudioService" /> class and checks
    ///     whether the bundled click sound exists.
    /// </summary>
    public LocalAudioService()
    {
        _isSoundAvailable = File.Exists(_soundPath);
    }

    /// <summary>Plays the click sound, silently disabling playback if it fails.</summary>
    public void PlayClickSound()
    {
        if (!_isSoundAvailable || _disposed) return;

        try
        {
            Play(_soundPath, _playbackLock, () => _disposed);
        }
        catch
        {
            _isSoundAvailable = false;
        }
    }

    /// <summary>Releases resources used by the audio service.</summary>
    public void Dispose()
    {
        lock (_playbackLock)
        {
            _disposed = true;
            if (OperatingSystem.IsWindows()) CloseWindowsAlias();
        }
    }

    private static void Play(string path, Lock playbackLock, Func<bool> isDisposed)
    {
        if (OperatingSystem.IsWindows())
            PlayWindows(path, playbackLock, isDisposed);
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

    private static void PlayWindows(string path, Lock playbackLock, Func<bool> isDisposed)
    {
        _ = Task.Run(() =>
        {
            lock (playbackLock)
            {
                if (isDisposed()) return;

                CloseWindowsAlias();
                var openResult = mciSendStringW($"open \"{path}\" type mpegvideo alias {MciAlias}", null, 0,
                    IntPtr.Zero);
                if (openResult != 0) return;

                // Wait for playback to finish before closing the alias: leaving it open
                // kept click.mp3 locked for the whole session and blocked updates.
                _ = mciSendStringW($"play {MciAlias} wait", null, 0, IntPtr.Zero);
                CloseWindowsAlias();
            }
        });
    }

    private static void CloseWindowsAlias()
    {
        _ = mciSendStringW($"close {MciAlias}", null, 0, IntPtr.Zero);
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendStringW(string command, System.Text.StringBuilder? returnString, int returnLength,
        IntPtr hwndCallback);
}
