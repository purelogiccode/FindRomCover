using MessagePack;

namespace FindRomCover.Models;

/// <summary>
///     A single machine entry from the MAME data set.
/// </summary>
[MessagePackObject]
public class MameData
{
    /// <summary>Short machine (ROM) name.</summary>
    [Key(0)]
    public string MachineName { get; set; } = string.Empty;

    /// <summary>Full machine description.</summary>
    [Key(1)]
    public string Description { get; set; } = string.Empty;
}