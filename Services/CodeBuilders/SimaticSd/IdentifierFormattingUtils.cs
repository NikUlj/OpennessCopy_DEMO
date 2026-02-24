#nullable enable
using System;

namespace OpennessCopy.Services.CodeBuilders.SimaticSd;

/// <summary>
/// Utility helpers for formatting SIMATIC SD identifiers consistently.
/// </summary>
public static class IdentifierFormattingUtils
{
    /// <summary>
    /// Formats an identifier, adding quotes when it contains dots or when a namespace-prefixed
    /// identifier requires quoting.
    /// </summary>
    public static string FormatIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        if (IsQuoted(name) || IsPrefixedAndQuoted(name))
        {
            return name;
        }

        const string namespacePrefix = "_.";
        if (name.StartsWith(namespacePrefix, StringComparison.Ordinal))
        {
            var remainder = name.Substring(namespacePrefix.Length);
            if (string.IsNullOrEmpty(remainder))
            {
                return namespacePrefix;
            }

            if (IsQuoted(remainder) || !RequiresQuoting(remainder))
            {
                return namespacePrefix + remainder;
            }

            return namespacePrefix + $"\"{remainder}\"";
        }

        return RequiresQuoting(name)
            ? $"\"{name}\""
            : name;
    }

    /// <summary>
    /// Determines whether an identifier needs quoting (currently checks for dots).
    /// </summary>
    private static bool RequiresQuoting(string value)
    {
        return value.Contains(".");
    }

    private static bool IsQuoted(string value)
    {
        return value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"';
    }

    private static bool IsPrefixedAndQuoted(string value)
    {
        const string namespacePrefix = "_.\"";
        return value.StartsWith(namespacePrefix, StringComparison.Ordinal) &&
               value.EndsWith("\"", StringComparison.Ordinal);
    }
}
