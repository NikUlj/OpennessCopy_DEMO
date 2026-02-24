namespace OpennessCopy.Models;

public class TagAddressReplacePair
{
    public string FindString { get; set; } = "";
    public string ReplaceString { get; set; } = "";
    public int DigitPosition { get; set; } = 1; // 1-based, right-to-left from decimal point
    public int LengthFilter { get; set; } = 1; // Only process addresses with this many digits

    public TagAddressReplacePair()
    {
    }

    public TagAddressReplacePair(string findString, string replaceString, int digitPosition, int lengthFilter)
    {
        FindString = findString ?? "";
        ReplaceString = replaceString ?? "";
        DigitPosition = digitPosition;
        LengthFilter = lengthFilter;
    }

    public override string ToString()
    {
        return $"'{FindString}' -> '{ReplaceString}' (Position: {DigitPosition}, Length: {LengthFilter})";
    }
}
