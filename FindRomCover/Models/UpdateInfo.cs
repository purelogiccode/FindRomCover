using System.Diagnostics.CodeAnalysis;

namespace FindRomCover.Models;

/// <summary>
///     Information about an available application update.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global",
    Justification = "Update information is mutable by design and tests exercise the setters.")]
public class UpdateInfo
{
    /// <summary>Whether a newer version is available.</summary>
    public bool IsUpdateAvailable { get; set; }

    /// <summary>Version of the currently installed application.</summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>Latest released version.</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>URL of the release page.</summary>
    public string ReleaseUrl { get; set; } = string.Empty;

    /// <summary>Release notes for the latest version.</summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>Publication date of the release.</summary>
    public string PublishedAt { get; set; } = string.Empty;
}
