using System;
using System.Collections.Generic;
using System.Linq;

namespace OpennessCopy.Services.CodeBuilders.SimaticSd;

public static class StatusDbEditor
{
    /// <summary>
    /// Appends status entries before PLC array, adjusts PLC bounds, or removes PLC if offset hits upper bound.
    /// Throws if offset would exceed 126.
    /// </summary>
    public static string AppendStatusEntries(
        string s7dclContent,
        IEnumerable<string> statusVariableLines,
        string plcArrayName = "PLC",
        int upperBound = 126,
        int bytesPerStatus = 2,
        int leadingBytes = 2)
    {
        var content = s7dclContent.Replace("\r\n", "\n");
        var lines = content.Split('\n').ToList();

        // Compute current offset based on existing status entries and leading bytes.
        int currentOffset = leadingBytes + CountExistingStatusEntries(lines, bytesPerStatus);
        var variableLines = statusVariableLines as string[] ?? statusVariableLines.ToArray();
        int newStatusCount = variableLines.Count();
        int newOffset = currentOffset + newStatusCount * bytesPerStatus;

        if (newOffset > upperBound)
        {
            throw new InvalidOperationException($"PLC array offset would exceed {upperBound} (offset={newOffset}).");
        }

        // Insert new status lines before PLC array.
        content = SimaticSdDbEditor.AppendVariables(
            content,
            variableLines,
            targetStructName: null,
            anchorVariableName: plcArrayName,
            insertBeforeAnchor: true);

        // Adjust or remove PLC array.
        if (newOffset == upperBound)
        {
            content = RemovePlcArray(content, plcArrayName);
        }
        else
        {
            content = UpdatePlcArrayLowerBound(content, plcArrayName, newOffset, upperBound);
        }

        return content;
    }

    private static int CountExistingStatusEntries(List<string> lines, int bytesPerStatus)
    {
        // Removed for demo build
        // return count * bytesPerStatus;
        return 0;
    }

    private static string RemovePlcArray(string content, string plcArrayName)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith(plcArrayName, StringComparison.OrdinalIgnoreCase) &&
                trimmed.Contains("Array"))
            {
                lines.RemoveAt(i);
                break;
            }
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string UpdatePlcArrayLowerBound(string content, string plcArrayName, int lowerBound, int upperBound)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith(plcArrayName, StringComparison.OrdinalIgnoreCase) &&
                trimmed.Contains("Array"))
            {
                var indent = lines[i].Substring(0, lines[i].Length - trimmed.Length);
                lines[i] = $"{indent}{plcArrayName} : Array[{lowerBound}..{upperBound}] of Byte;";
                break;
            }
        }
        return string.Join(Environment.NewLine, lines);
    }
}
