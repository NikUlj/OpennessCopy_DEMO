using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static OpennessCopy.Services.CodeBuilders.SimaticSd.IdentifierFormattingUtils;

#nullable enable

namespace OpennessCopy.Services.CodeBuilders.SimaticSd;

public sealed partial class SimaticSdBuilder
{
    public static SimaticSdUserDefinedTypeBuilder CreateUserDefinedType(string name)
    {
        return new SimaticSdUserDefinedTypeBuilder(name);
    }

    public sealed class SimaticSdUserDefinedTypeBuilder
    {
        private readonly string _name;
        private readonly List<DeclarationEntry> _members = new();
        private readonly Dictionary<string, MultiLingualTextEntry> _texts = new(StringComparer.OrdinalIgnoreCase);
        private int _nextTextId = 1;

        internal SimaticSdUserDefinedTypeBuilder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Type name must be provided.", nameof(name));
            }

            _name = name;
        }

        public SimaticSdUserDefinedTypeBuilder AddMember(
            string name,
            string type,
            string? defaultValue = null,
            MultiLingualText? comment = null)
        {
            ValidateMember(name, type);
            var commentId = RegisterText(comment);
            _members.Add(new DeclarationEntry(name, type, defaultValue, commentId));
            return this;
        }

        public SimaticSdUserDefinedType Build()
        {
            if (_members.Count == 0)
            {
                throw new InvalidOperationException($"UDT '{_name}' requires at least one member.");
            }

            return new SimaticSdUserDefinedType(_name, _members.ToArray());
        }

        public SimaticSdTextResource BuildTextResource()
        {
            var entries = _texts.Values.Select(entry => entry.Clone()).ToList();
            return new SimaticSdTextResource(entries);
        }

        private void ValidateMember(string name, string type)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Member name must be provided.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException("Member type must be provided.", nameof(type));
            }
        }

        private string? RegisterText(MultiLingualText? text)
        {
            if (text == null || text.IsEmpty)
            {
                return null;
            }

            var id = $"MLC_{_nextTextId++:0000}";
            var entry = new MultiLingualTextEntry(id);
            foreach (var translation in text.Translations)
            {
                entry.SetTranslation(translation.Key, translation.Value);
            }

            _texts[id] = entry;
            return id;
        }
    }

    public sealed class SimaticSdUserDefinedType
    {
        private readonly string _name;
        private readonly IReadOnlyList<DeclarationEntry> _members;

        internal SimaticSdUserDefinedType(string name, IReadOnlyList<DeclarationEntry> members)
        {
            _name = name;
            _members = members;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine("TYPE");
            builder.AppendLine($"    {FormatIdentifier(_name)} : STRUCT");

            foreach (var member in _members)
            {
                if (!string.IsNullOrWhiteSpace(member.CommentId))
                {
                    builder.AppendLine($"        {{ S7_MLC := \"{member.CommentId}\" }}");
                }

                var identifier = FormatIdentifier(member.Name);
                var line = $"        {identifier} : {member.Type}";
                if (!string.IsNullOrWhiteSpace(member.DefaultValue))
                {
                    line += $" := {member.DefaultValue}";
                }

                builder.AppendLine(line + ";");
            }

            builder.AppendLine("    END_STRUCT;");
            builder.AppendLine("END_TYPE");
            return builder.ToString();
        }
    }
}
