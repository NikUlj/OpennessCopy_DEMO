using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable enable

namespace OpennessCopy.Services.CodeBuilders.SimaticSd;

public sealed partial class SimaticSdBuilder
{
    internal sealed class MultiLingualTextEntry(string id)
    {
        private readonly Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);

        public string Id { get; } = id;

        public IReadOnlyDictionary<string, string> Translations => _translations;

        public void SetTranslation(string language, string value)
        {
            _translations[language] = value;
        }

        public MultiLingualTextEntry Clone()
        {
            var clone = new MultiLingualTextEntry(Id);
            foreach (var kvp in _translations)
            {
                clone._translations[kvp.Key] = kvp.Value;
            }

            return clone;
        }
    }

    public sealed class SimaticSdTextResource
    {
        private readonly IReadOnlyList<MultiLingualTextEntry> _entries;

        internal SimaticSdTextResource(IEnumerable<MultiLingualTextEntry> entries)
        {
            _entries = new List<MultiLingualTextEntry>(entries);
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine("MultiLingualTexts:");

            foreach (var entry in _entries.OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"  - id: {entry.Id}");
                foreach (var translation in entry.Translations.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var language = translation.Key;
                    var text = translation.Value ?? string.Empty;
                    if (text.Contains('\n'))
                    {
                        builder.AppendLine($"    {language}: |+");
                        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
                        foreach (var line in lines)
                        {
                            builder.AppendLine($"      {line}");
                        }
                    }
                    else
                    {
                        builder.AppendLine($"    {language}: {QuoteSingleLine(text)}");
                    }
                }
            }

            return builder.ToString();
        }

        private static string QuoteSingleLine(string value)
        {
            var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"\"{escaped}\"";
        }
    }
}
