using System.Collections.Generic;
using System.Linq;
using OpennessCopy.Models;

namespace OpennessCopy.Utils;

public static class TagAddressAnalyzer
{
    /// <summary>
    /// Extracts digit count from a tag address like %MW1234.5 (returns 4)
    /// </summary>
    private static int ExtractAddressDigitCount(string address)
    {
        if (string.IsNullOrEmpty(address) || !address.Contains("%") || !address.Contains("."))
            return 0;

        try
        {
            int percentIndex = address.IndexOf('%');
            int dotIndex = address.IndexOf('.');
            
            if (dotIndex > percentIndex + 1)
            {
                // Find where the prefix ends and digits begin
                int digitStartIndex = percentIndex + 1;
                while (digitStartIndex < dotIndex && !char.IsDigit(address[digitStartIndex]))
                {
                    digitStartIndex++;
                }
                
                if (digitStartIndex < dotIndex)
                {
                    string digits = address.Substring(digitStartIndex, dotIndex - digitStartIndex);
                    return digits.Length;
                }
            }
        }
        catch
        {
            // Return 0 if parsing fails
        }

        return 0;
    }

    /// <summary>
    /// Finds up to one example each of 2, 3, and 4-digit addresses from tag data
    /// </summary>
    public static List<TagExample> FindExamplesByDigitSize(List<TagExample> allTags)
    {
        if (allTags == null || allTags.Count == 0)
            return new List<TagExample>();

        var examples = new List<TagExample>();
        var targetSizes = new[] { 2, 3, 4 };
        
        foreach (int targetSize in targetSizes)
        {
            var example = allTags.FirstOrDefault(tag => tag.DigitCount == targetSize);
            if (example != null)
            {
                examples.Add(example);
            }
        }

        return examples;
    }

    /// <summary>
    /// Analyzes tag data and returns both available digit lengths and representative examples
    /// </summary>
    public static (List<int> AvailableDigitLengths, List<TagExample> Examples) AnalyzeTagsWithDigitVariations(List<(string Name, string Address)> tagData)
    {
        if (tagData == null || tagData.Count == 0)
            return ([1, 2, 3, 4, 5], GetDefaultExamples());

        // Group tags by digit count and get first example of each
        var tagsByDigitCount = new Dictionary<int, TagExample>();
        
        foreach (var (name, address) in tagData)
        {
            int digitCount = ExtractAddressDigitCount(address);
            if (digitCount > 0 && !tagsByDigitCount.ContainsKey(digitCount))
            {
                tagsByDigitCount[digitCount] = new TagExample
                {
                    Name = name,
                    Address = address,
                    DigitCount = digitCount
                };
            }
        }

        var availableDigitLengths = tagsByDigitCount.Keys.OrderBy(k => k).ToList();
        var examples = tagsByDigitCount.Values.OrderBy(e => e.DigitCount).ToList();

        return (availableDigitLengths, examples);
    }

    private static List<TagExample> GetDefaultExamples()
    {
        return
        [
            new TagExample { Name = "Motor_01_Start", Address = "%M20.4", DigitCount = 2 },
            new TagExample { Name = "Valve_ABC_Status", Address = "%M127.0", DigitCount = 3 },
            new TagExample { Name = "Conveyor_Main_Speed", Address = "%M1920.4", DigitCount = 4 }
        ];
    }

    /// <summary>
    /// Gets the set of available digit lengths from sample tag data
    /// </summary>
    public static List<int> GetAvailableDigitLengths(List<TagExample> sampleTags)
    {
        if (sampleTags == null || sampleTags.Count == 0)
            return [1, 2, 3, 4, 5]; // Default options if no data available

        return sampleTags.Select(tag => tag.DigitCount)
                         .Where(count => count > 0)
                         .Distinct()
                         .OrderBy(count => count)
                         .ToList();
    }

    /// <summary>
    /// Gets the set of available digit lengths from a list of integer addresses
    /// Used for ET module address configuration
    /// </summary>
    public static List<int> GetAvailableDigitLengthsFromIntegers(List<int> addresses)
    {
        if (addresses == null || addresses.Count == 0)
            return [1, 2, 3, 4, 5]; // Default options if no data available

        return addresses.Select(addr => addr.ToString().Length)
                       .Where(count => count > 0)
                       .Distinct()
                       .OrderBy(count => count)
                       .ToList();
    }

    /// <summary>
    /// Gets valid positions for a given digit length (1-based, right-to-left)
    /// </summary>
    public static List<int> GetValidPositionsForLength(int digitLength)
    {
        if (digitLength <= 0)
            return new List<int>();

        var positions = new List<int>();
        for (int i = 1; i <= digitLength; i++)
        {
            positions.Add(i);
        }
        return positions;
    }

    /// <summary>
    /// Analyzes a list of tag names and addresses and returns TagExample objects with calculated digit counts
    /// </summary>
    public static List<TagExample> AnalyzeTags(List<(string Name, string Address)> tagData)
    {
        var examples = new List<TagExample>();
        
        foreach (var (name, address) in tagData)
        {
            int digitCount = ExtractAddressDigitCount(address);
            if (digitCount > 0) // Only include tags with valid addresses
            {
                examples.Add(new TagExample
                {
                    Name = name,
                    Address = address,
                    DigitCount = digitCount
                });
            }
        }

        return examples;
    }
}