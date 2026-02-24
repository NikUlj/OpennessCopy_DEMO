using System;
using System.Collections.Generic;

namespace OpennessCopy.Services.CodeBuilders;

#nullable enable

public sealed class MultiLingualText
{
    private const string DefaultLanguage = "sl-SI";
    private readonly Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);

    public static MultiLingualText From(string language, string value)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language must be provided.", nameof(language));
        }

        return new MultiLingualText().With(language, value);
    }

    public static implicit operator MultiLingualText?(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : From(DefaultLanguage, value!);
    }

    public MultiLingualText With(string language, string value)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language must be provided.", nameof(language));
        }

        _translations[language] = value;
        return this;
    }

    internal bool IsEmpty => _translations.Count == 0;

    internal IReadOnlyDictionary<string, string> Translations => _translations;
}
