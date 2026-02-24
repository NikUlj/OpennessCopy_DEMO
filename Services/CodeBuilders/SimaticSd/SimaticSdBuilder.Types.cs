#nullable enable

using System.Collections.Generic;

namespace OpennessCopy.Services.CodeBuilders.SimaticSd;

public sealed partial class SimaticSdBuilder
{
    internal sealed class DeclarationEntry
    {
        internal DeclarationEntry(
            string name,
            string type,
            string? defaultValue,
            string? commentId,
            DeclarationEntryKind kind = DeclarationEntryKind.Scalar,
            IReadOnlyList<DeclarationEntry>? members = null,
            IReadOnlyList<DeclarationAttribute>? attributes = null)
        {
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
            CommentId = commentId;
            Kind = kind;
            Members = members ?? [];
            Attributes = attributes ?? [];
        }

        public string Name { get; }

        public string Type { get; }

        public string? DefaultValue { get; }

        public string? CommentId { get; }

        public DeclarationEntryKind Kind { get; }

        public IReadOnlyList<DeclarationEntry> Members { get; }

        public IReadOnlyList<DeclarationAttribute> Attributes { get; }

        public bool IsStruct => Kind == DeclarationEntryKind.Struct;
    }
    internal sealed class DeclarationAttribute(string name, string value)
    {
        public string Name { get; } = name;
        public string Value { get; } = value;
    }

    internal enum DeclarationEntryKind
    {
        Scalar,
        Struct
    }

    internal sealed class NetworkEntry(string body, string? titleId, string? commentId, string? language)
    {
        public string Body { get; } = body;

        public string? TitleId { get; } = titleId;

        public string? CommentId { get; } = commentId;

        public string? Language { get; } = language;
    }

    internal sealed class BlockMetadata(
        string name,
        int? blockNumber,
        string? preferredLanguage,
        bool optimized,
        string? version,
        string? editorMode,
        bool? standardRetain,
        string? returnType)
    {
        public string Name { get; } = name;

        public int? BlockNumber { get; } = blockNumber;

        public string? PreferredLanguage { get; } = preferredLanguage;

        public bool Optimized { get; } = optimized;

        public string? Version { get; } = version;

        public string? EditorMode { get; } = editorMode;

        public bool? StandardRetain { get; } = standardRetain;

        public string? ReturnType { get; } = returnType;
    }
}
