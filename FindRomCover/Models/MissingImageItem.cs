namespace FindRomCover.Models;

public class MissingImageItem
{
    public MissingImageItem(string romName, string searchName)
    {
        RomName = romName;
        SearchName = searchName;
    }

    public string RomName { get; }
    public string SearchName { get; }

    public override string ToString()
    {
        return RomName;
    }
}