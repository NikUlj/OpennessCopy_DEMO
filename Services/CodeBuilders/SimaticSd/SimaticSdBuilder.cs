using System;
using System.Collections.Generic;
using static OpennessCopy.Services.CodeBuilders.SimaticSd.IdentifierFormattingUtils;

#nullable enable

namespace OpennessCopy.Services.CodeBuilders.SimaticSd
{
    /// <summary>
    /// Fluent builder for SIMATIC SD (FBD/LAD/SCL/DB) artifacts.
    /// Produces deterministic text output that can be written directly to .s7dcl files.
    /// </summary>
    public sealed partial class SimaticSdBuilder
    {
        private readonly SimaticSdBlockType _blockType;
        private readonly BlockMetadata _metadata;

        private readonly List<DeclarationEntry> _inputs = new();
        private readonly List<DeclarationEntry> _outputs = new();
        private readonly List<DeclarationEntry> _inOuts = new();
        private readonly List<DeclarationEntry> _statics = new();
        private readonly List<DeclarationEntry> _temps = new();
        private readonly List<DeclarationEntry> _constants = new();
        private readonly List<NetworkEntry> _networks = new();
        private readonly List<string> _rawDeclarationSections = new();
        private readonly List<string> _dataBlockInitializers = new();
        private readonly Dictionary<string, MultiLingualTextEntry> _texts = new(StringComparer.OrdinalIgnoreCase);
        private int _nextTextId = 1;

        private SimaticSdBuilder(SimaticSdBlockType blockType, BlockMetadata metadata)
        {
            _blockType = blockType;
            _metadata = metadata;
        }

        public static SimaticSdBuilder CreateBlock(
            string name,
            int? blockNumber = null,
            string? preferredLanguage = "FBD",
            bool optimized = true,
            string? version = "0.1")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Block name must be provided.", nameof(name));
            }

            var metadata = new BlockMetadata(
                name,
                blockNumber,
                preferredLanguage ?? "FBD",
                optimized,
                version ?? "0.1",
                editorMode: null,
                standardRetain: null,
                returnType: null);

            return new SimaticSdBuilder(SimaticSdBlockType.FunctionBlock, metadata);
        }

        public static SimaticSdBuilder CreateFunction(
            string name,
            int? blockNumber = null,
            string returnType = "Void",
            bool optimized = true,
            string? version = "0.1",
            string? preferredLanguage = "FBD",
            string? editorMode = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Block name must be provided.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(returnType))
            {
                throw new ArgumentException("Return type must be provided.", nameof(returnType));
            }

            var metadata = new BlockMetadata(
                name,
                blockNumber,
                preferredLanguage: preferredLanguage,
                optimized,
                version ?? "0.1",
                editorMode,
                standardRetain: null,
                returnType);

            return new SimaticSdBuilder(SimaticSdBlockType.Function, metadata);
        }

        public static SimaticSdBuilder CreateDataBlock(
            string name,
            int? blockNumber = null,
            bool optimized = true,
            bool standardRetain = false,
            string? version = "0.1")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Block name must be provided.", nameof(name));
            }

            var metadata = new BlockMetadata(
                name,
                blockNumber,
                preferredLanguage: null,
                optimized,
                version ?? "0.1",
                editorMode: null,
                standardRetain: standardRetain,
                returnType: null);

            return new SimaticSdBuilder(SimaticSdBlockType.DataBlock, metadata);
        }

        public static SimaticSdInstanceDataBlockBuilder CreateInstanceDataBlock(
            string name,
            string instanceType,
            int? blockNumber = null,
            bool optimized = false,
            bool standardRetain = false,
            string? version = "0.1")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Block name must be provided.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(instanceType))
            {
                throw new ArgumentException("Instance type must be provided.", nameof(instanceType));
            }

            return new SimaticSdInstanceDataBlockBuilder(name, blockNumber, instanceType, optimized, standardRetain, version ?? "0.1");
        }

        public SimaticSdBuilder AddInput(string name, string type, string? defaultValue = null, MultiLingualText? comment = null, Action<DeclarationBuilder>? configure = null)
        {
            _inputs.Add(BuildScalarEntry(name, type, defaultValue, comment, configure));
            return this;
        }

        public SimaticSdBuilder AddOutput(string name, string type, string? defaultValue = null, MultiLingualText? comment = null, Action<DeclarationBuilder>? configure = null)
        {
            _outputs.Add(BuildScalarEntry(name, type, defaultValue, comment, configure));
            return this;
        }

        public SimaticSdBuilder AddInOut(string name, string type, string? defaultValue = null, MultiLingualText? comment = null, Action<DeclarationBuilder>? configure = null)
        {
            _inOuts.Add(BuildScalarEntry(name, type, defaultValue, comment, configure));
            return this;
        }

        public SimaticSdBuilder AddStatic(string name, string type, string? defaultValue = null, MultiLingualText? comment = null, Action<DeclarationBuilder>? configure = null)
        {
            _statics.Add(BuildScalarEntry(name, type, defaultValue, comment, configure));
            return this;
        }

        public SimaticSdBuilder AddTemp(string name, string type, string? defaultValue = null, MultiLingualText? comment = null, Action<DeclarationBuilder>? configure = null)
        {
            _temps.Add(BuildScalarEntry(name, type, defaultValue, comment, configure));
            return this;
        }

        public SimaticSdBuilder AddConstant(string name, string type, string? defaultValue = null, MultiLingualText? comment = null, Action<DeclarationBuilder>? configure = null)
        {
            _constants.Add(BuildScalarEntry(name, type, defaultValue, comment, configure));
            return this;
        }

        public SimaticSdBuilder AddInputStruct(string name, Action<StructDeclarationBuilder> configure, MultiLingualText? comment = null, Action<DeclarationBuilder>? declarationConfigure = null)
        {
            _inputs.Add(BuildStructEntry(name, comment, configure, declarationConfigure));
            return this;
        }

        public SimaticSdBuilder AddOutputStruct(string name, Action<StructDeclarationBuilder> configure, MultiLingualText? comment = null, Action<DeclarationBuilder>? declarationConfigure = null)
        {
            _outputs.Add(BuildStructEntry(name, comment, configure, declarationConfigure));
            return this;
        }

        public SimaticSdBuilder AddInOutStruct(string name, Action<StructDeclarationBuilder> configure, MultiLingualText? comment = null, Action<DeclarationBuilder>? declarationConfigure = null)
        {
            _inOuts.Add(BuildStructEntry(name, comment, configure, declarationConfigure));
            return this;
        }

        public SimaticSdBuilder AddStaticStruct(string name, Action<StructDeclarationBuilder> configure, MultiLingualText? comment = null, Action<DeclarationBuilder>? declarationConfigure = null)
        {
            _statics.Add(BuildStructEntry(name, comment, configure, declarationConfigure));
            return this;
        }

        public SimaticSdBuilder AddTempStruct(string name, Action<StructDeclarationBuilder> configure, MultiLingualText? comment = null, Action<DeclarationBuilder>? declarationConfigure = null)
        {
            _temps.Add(BuildStructEntry(name, comment, configure, declarationConfigure));
            return this;
        }

        public SimaticSdBuilder AddConstantStruct(string name, Action<StructDeclarationBuilder> configure, MultiLingualText? comment = null, Action<DeclarationBuilder>? declarationConfigure = null)
        {
            _constants.Add(BuildStructEntry(name, comment, configure, declarationConfigure));
            return this;
        }

        public SimaticSdBuilder AddNetwork(
            string body,
            MultiLingualText? title = null,
            MultiLingualText? comment = null,
            string? language = null)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentException("Network body must be provided.", nameof(body));
            }

            var titleId = RegisterText(title);
            var commentId = RegisterText(comment);

            _networks.Add(new NetworkEntry(body, titleId, commentId, language));
            return this;
        }

        /// <summary>
        /// Appends a raw declaration section (for example, a complete VAR...END_VAR block) directly into the block.
        /// </summary>
        public SimaticSdBuilder AddDeclarationSection(string declarationBlock)
        {
            if (string.IsNullOrWhiteSpace(declarationBlock))
            {
                throw new ArgumentException("Declaration block must be provided.", nameof(declarationBlock));
            }

            _rawDeclarationSections.Add(declarationBlock);
            return this;
        }

        public SimaticSdBuilder AddDataBlockInitializer(string assignmentLine)
        {
            if (_blockType != SimaticSdBlockType.DataBlock)
            {
                throw new InvalidOperationException("Data block initializers can only be added to DATA_BLOCK builders.");
            }

            if (string.IsNullOrWhiteSpace(assignmentLine))
            {
                throw new ArgumentException("Initializer text must be provided.", nameof(assignmentLine));
            }

            _dataBlockInitializers.Add(assignmentLine.Trim());
            return this;
        }

        public SimaticSdBlock Build()
        {
            return new SimaticSdBlock(
                _blockType,
                _metadata,
                _inputs.ToArray(),
                _outputs.ToArray(),
                _inOuts.ToArray(),
                _statics.ToArray(),
                _temps.ToArray(),
                _constants.ToArray(),
                _networks.ToArray(),
                _rawDeclarationSections.ToArray(),
                _dataBlockInitializers.ToArray());
        }

        public SimaticSdTextResource BuildTextResource()
        {
            var entries = new List<MultiLingualTextEntry>(_texts.Count);
            foreach (var entry in _texts.Values)
            {
                entries.Add(entry.Clone());
            }

            return new SimaticSdTextResource(entries);
        }
        
        private DeclarationEntry BuildScalarEntry(
            string name,
            string type,
            string? defaultValue,
            MultiLingualText? comment,
            Action<DeclarationBuilder>? configure)
        {
            var attributes = BuildAttributes(name, type, defaultValue, comment, configure);
            return CreateScalarEntry(name, type, defaultValue, comment, attributes);
        }

        private DeclarationEntry BuildStructEntry(
            string name,
            MultiLingualText? comment,
            Action<StructDeclarationBuilder> configureMembers,
            Action<DeclarationBuilder>? configureDeclaration)
        {
            var attributes = BuildAttributes(name, "STRUCT", null, comment, configureDeclaration);
            return CreateStructEntry(name, comment, configureMembers, attributes);
        }

        private DeclarationEntry CreateScalarEntry(string name, string type, string? defaultValue, MultiLingualText? comment, IReadOnlyList<DeclarationAttribute>? attributes = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Variable name must be provided.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException("Variable type must be provided.", nameof(type));
            }

            var commentId = RegisterText(comment);
            return new DeclarationEntry(name, type, defaultValue, commentId, DeclarationEntryKind.Scalar, members: null, attributes: attributes);
        }

        private DeclarationEntry CreateStructEntry(string name, MultiLingualText? comment, Action<StructDeclarationBuilder> configure, IReadOnlyList<DeclarationAttribute>? attributes = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Struct name must be provided.", nameof(name));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var members = new List<DeclarationEntry>();
            var structBuilder = new StructDeclarationBuilder(this, members);
            configure(structBuilder);

            if (members.Count == 0)
            {
                throw new InvalidOperationException($"Struct '{name}' must declare at least one member.");
            }

            var commentId = RegisterText(comment);
            return new DeclarationEntry(
                name,
                "STRUCT",
                defaultValue: null,
                commentId,
                DeclarationEntryKind.Struct,
                members.ToArray(),
                attributes);
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

        public sealed class StructDeclarationBuilder
        {
            private readonly SimaticSdBuilder _owner;
            private readonly List<DeclarationEntry> _members;

            internal StructDeclarationBuilder(SimaticSdBuilder owner, List<DeclarationEntry> members)
            {
                _owner = owner;
                _members = members;
            }

            public StructDeclarationBuilder AddMember(string name, string type, string? defaultValue = null, MultiLingualText? comment = null, Action<DeclarationBuilder>? configure = null)
            {
                _members.Add(_owner.BuildScalarEntry(name, type, defaultValue, comment, configure));
                return this;
            }

            public StructDeclarationBuilder AddStruct(string name, Action<StructDeclarationBuilder> configure, MultiLingualText? comment = null, Action<DeclarationBuilder>? declarationConfigure = null)
            {
                _members.Add(_owner.BuildStructEntry(name, comment, configure, declarationConfigure));
                return this;
            }
        }

        public sealed class DeclarationBuilder
        {
            private readonly List<DeclarationAttribute> _attributes = new();

            internal DeclarationBuilder(string name, string type, string? defaultValue, MultiLingualText? comment)
            {
                Name = name;
                Type = type;
                DefaultValue = defaultValue;
                Comment = comment;
            }

            public string Name { get; }

            public string Type { get; }

            public string? DefaultValue { get; }

            public MultiLingualText? Comment { get; }

            internal IReadOnlyList<DeclarationAttribute> Attributes => _attributes;

            public DeclarationBuilder WithAttribute(string name, string value)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Attribute name must be provided.", nameof(name));
                }

                _attributes.Add(new DeclarationAttribute(name, value));
                return this;
            }
        }

        private IReadOnlyList<DeclarationAttribute>? BuildAttributes(
            string name,
            string type,
            string? defaultValue,
            MultiLingualText? comment,
            Action<DeclarationBuilder>? configure)
        {
            if (configure == null)
            {
                return null;
            }

            var builder = new DeclarationBuilder(name, type, defaultValue, comment);
            configure(builder);
            return builder.Attributes.Count == 0 ? null : new List<DeclarationAttribute>(builder.Attributes);
        }

        private static string FormatTypeDescriptor(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return type;
            }

            const string namespacePrefix = "_.";
            return type.StartsWith(namespacePrefix, StringComparison.Ordinal)
                ? FormatIdentifier(type)
                : type;
        }
    }
}
