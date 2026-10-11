namespace FindRomCover.Models;

/// <summary>
///     A ROM entry that has no matching cover image, used by the missing images list
///     and the AI batch fill.
/// </summary>
public class MissingImageItem
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MissingImageItem" /> class.
    /// </summary>
    /// <param name="romName">Original ROM name.</param>
    /// <param name="searchName">Cleaned name used for search queries.</param>
    public MissingImageItem(string romName, string searchName)
    {
        RomName = romName;
        SearchName = searchName;
    }

    /// <summary>Original ROM name.</summary>
    public string RomName { get; }
    /// <summary>Cleaned name used for search queries.</summary>
    public string SearchName { get; }

    /// <summary>
    ///     Returns the ROM name.
    /// </summary>
    /// <returns>The value of <see cref="RomName" />.</returns>
    public override string ToString()
    {
        return RomName;
    }
}
