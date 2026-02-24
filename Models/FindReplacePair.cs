namespace OpennessCopy.Models;

public class FindReplacePair(string findString, string replaceString)
{
    public string FindString { get; set; } = findString ?? "";
    public string ReplaceString { get; set; } = replaceString ?? "";

    public override string ToString()
    {
        return $"'{FindString}' -> '{ReplaceString}'";
    }
}
