#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpennessCopy.Services.CodeBuilders.SimaticSd;

/// <summary>
/// Lightweight SD editor for appending variables to DB blocks.
/// Works on exported .s7dcl text; .s7res is ignored.
/// </summary>
public static class SimaticSdDbEditor
{
    public static string AppendVariables(
        string s7dclContent,
        IEnumerable<string> variableLines,
        string? targetStructName = null,
        string? anchorVariableName = null,
        bool insertBeforeAnchor = true)
    {
        if (string.IsNullOrWhiteSpace(s7dclContent))
        {
            throw new ArgumentException("s7dclContent must be provided.", nameof(s7dclContent));
        }

        var vars = variableLines?.ToList() ?? [];
        if (vars.Count == 0)
        {
            return s7dclContent;
        }

        var lines = s7dclContent.Replace("\r\n", "\n").Split('\n').ToList();
        var insertIndex = -1;
        var indent = "    ";
        var structDepth = 0;
        var targetDepth = -1;
        var target = targetStructName?.Trim();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (TryParseStructName(trimmed, out var structName))
            {
                structDepth++;
                if (!string.IsNullOrEmpty(target) &&
                    targetDepth == -1 &&
                    string.Equals(structName, target, StringComparison.Ordinal))
                {
                    targetDepth = structDepth;
                }
            }

            if (trimmed.StartsWith("END_STRUCT", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(target) && targetDepth == structDepth)
                {
                    insertIndex = i;
                    indent = line.Substring(0, line.Length - trimmed.Length);
                    break;
                }

                structDepth = Math.Max(0, structDepth - 1);
            }

            if (string.IsNullOrEmpty(target) &&
                structDepth == 0 &&
                trimmed.StartsWith("END_VAR", StringComparison.OrdinalIgnoreCase))
            {
                insertIndex = i;
                indent = line.Substring(0, line.Length - trimmed.Length);
                break;
            }

            if (string.IsNullOrEmpty(target) && structDepth == 0 && !string.IsNullOrEmpty(anchorVariableName))
            {
                if (IsVariableLine(trimmed, anchorVariableName!))
                {
                    insertIndex = insertBeforeAnchor ? i : i + 1;
                    indent = line.Substring(0, line.Length - trimmed.Length);
                    break;
                }
            }
        }

        if (insertIndex == -1)
        {
            if (string.IsNullOrEmpty(target))
            {
                throw new InvalidOperationException("Could not find END_VAR or anchor to append variables.");
            }

            // Struct not found: append a new struct at the end of root VAR, just before END_VAR
            insertIndex = FindEndVarIndex(lines);
            if (insertIndex == -1)
            {
                throw new InvalidOperationException("Could not find END_VAR to append new struct.");
            }

            var newStructLines = new List<string>
            {
                $"{indent}{target} : STRUCT"
            };
            newStructLines.AddRange(vars.Select(v => $"{indent}    {v.TrimEnd()}"));
            newStructLines.Add($"{indent}END_STRUCT;");

            lines.InsertRange(insertIndex, newStructLines);
            return string.Join(Environment.NewLine, lines);
        }

        var newLines = vars.Select(v => $"{indent}{v.TrimEnd()}");
        lines.InsertRange(insertIndex, newLines);

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsVariableLine(string trimmedLine, string variableName)
    {
        var namePortion = variableName.Trim().Trim('"');
        if (string.IsNullOrEmpty(namePortion))
        {
            return false;
        }

        if (!trimmedLine.Contains(":"))
        {
            return false;
        }

        var candidate = trimmedLine.Split(':')[0].Trim().Trim('"');
        return string.Equals(candidate, namePortion, StringComparison.OrdinalIgnoreCase);
    }

    private static int FindEndVarIndex(IReadOnlyList<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("END_VAR", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParseStructName(string trimmedLine, out string structName)
    {
        structName = string.Empty;
        if (!trimmedLine.Contains("STRUCT"))
        {
            return false;
        }

        var colonIndex = trimmedLine.IndexOf(':');
        if (colonIndex <= 0)
        {
            return false;
        }

        var namePart = trimmedLine.Substring(0, colonIndex).Trim();
        if (string.IsNullOrEmpty(namePart))
        {
            return false;
        }

        structName = namePart.Trim('"');
        return true;
    }
}
