using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#nullable enable

namespace OpennessCopy.Services.CodeBuilders.SimaticSd;

public static class TemplateRenderer
{
    private static readonly Regex TokenRegex = new(@"{{\s*(\w+)\s*}}", RegexOptions.Compiled);

    public static string Render(string template, IReadOnlyDictionary<string, string> parameters)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        var replacements = parameters ?? throw new ArgumentNullException(nameof(parameters));

        return TokenRegex.Replace(
            template,
            match =>
            {
                var key = match.Groups[1].Value;
                if (!replacements.TryGetValue(key, out var value))
                {
                    throw new InvalidOperationException($"Template token '{key}' is not provided.");
                }

                return value;
            });
    }
}