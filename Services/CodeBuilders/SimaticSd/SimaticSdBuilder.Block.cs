using System;
using System.Collections.Generic;
using System.Text;
using static OpennessCopy.Services.CodeBuilders.SimaticSd.IdentifierFormattingUtils;

#nullable enable

namespace OpennessCopy.Services.CodeBuilders.SimaticSd;

public sealed partial class SimaticSdBuilder
{
    public sealed class SimaticSdBlock
    {
        private readonly SimaticSdBlockType _blockType;
        private readonly BlockMetadata _metadata;
        private readonly IReadOnlyList<DeclarationEntry> _inputs;
        private readonly IReadOnlyList<DeclarationEntry> _outputs;
        private readonly IReadOnlyList<DeclarationEntry> _inOuts;
        private readonly IReadOnlyList<DeclarationEntry> _statics;
        private readonly IReadOnlyList<DeclarationEntry> _temps;
        private readonly IReadOnlyList<DeclarationEntry> _constants;
        private readonly IReadOnlyList<NetworkEntry> _networks;
        private readonly IReadOnlyList<string> _rawDeclarationSections;
        private readonly IReadOnlyList<string> _dataBlockInitializers;

        internal SimaticSdBlock(
            SimaticSdBlockType blockType,
            BlockMetadata metadata,
            IReadOnlyList<DeclarationEntry> inputs,
            IReadOnlyList<DeclarationEntry> outputs,
            IReadOnlyList<DeclarationEntry> inOuts,
            IReadOnlyList<DeclarationEntry> statics,
            IReadOnlyList<DeclarationEntry> temps,
            IReadOnlyList<DeclarationEntry> constants,
            IReadOnlyList<NetworkEntry> networks,
            IReadOnlyList<string> rawDeclarationSections,
            IReadOnlyList<string> dataBlockInitializers)
        {
            _blockType = blockType;
            _metadata = metadata;
            _inputs = inputs;
            _outputs = outputs;
            _inOuts = inOuts;
            _statics = statics;
            _temps = temps;
            _constants = constants;
            _networks = networks;
            _rawDeclarationSections = rawDeclarationSections;
            _dataBlockInitializers = dataBlockInitializers;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            AppendHeader(builder);
            AppendDeclarationSection(builder, "VAR_INPUT", _inputs);
            AppendDeclarationSection(builder, "VAR_OUTPUT", _outputs);
            AppendDeclarationSection(builder, "VAR_IN_OUT", _inOuts);
            var isDataBlock = _blockType == SimaticSdBlockType.DataBlock;
            var staticSectionKeyword = isDataBlock && _metadata.StandardRetain == true
                ? "VAR RETAIN"
                : "VAR";
            AppendDeclarationSection(
                builder,
                staticSectionKeyword,
                _statics,
                emitTrailingInitializer: isDataBlock,
                suppressInlineDefaults: isDataBlock);
            AppendDeclarationSection(builder, "VAR_TEMP", _temps);
            AppendDeclarationSection(builder, "VAR CONSTANT", _constants);
            AppendRawDeclarationSections(builder);
            AppendNetworks(builder);
            builder.AppendLine($"END_{GetBlockKeyword()}");
            return builder.ToString();
        }

        private void AppendHeader(StringBuilder builder)
        {
            builder.AppendLine("{");

            var entries = new List<string>();

            if (_metadata.BlockNumber.HasValue && _metadata.BlockNumber.Value != 0)
            {
                entries.Add($"S7_BlockNumber := \"{_metadata.BlockNumber.Value}\"");
            }

            entries.Add($"S7_Optimized := \"{(_metadata.Optimized ? "TRUE" : "FALSE")}\"");

            if (string.IsNullOrWhiteSpace(_metadata.EditorMode) &&
                !string.IsNullOrWhiteSpace(_metadata.PreferredLanguage))
            {
                entries.Add($"S7_PreferredLanguage := \"{_metadata.PreferredLanguage}\"");
            }

            if (!string.IsNullOrWhiteSpace(_metadata.EditorMode))
            {
                entries.Add($"S7_EditorMode := \"{_metadata.EditorMode}\"");
            }

            if (_metadata.StandardRetain.HasValue)
            {
                entries.Add($"S7_StandardRetain := \"{(_metadata.StandardRetain.Value ? "TRUE" : "FALSE")}\"");
            }

            entries.Add($"S7_Version := \"{_metadata.Version}\"");

            for (var i = 0; i < entries.Count; i++)
            {
                var suffix = i == entries.Count - 1 ? string.Empty : ";";
                builder.AppendLine($"    {entries[i]}{suffix}");
            }

            builder.AppendLine("}");
            builder.Append(GetBlockKeyword());
            builder.Append($" \"{_metadata.Name}\"");

            if (_blockType == SimaticSdBlockType.Function)
            {
                var returnType = string.IsNullOrWhiteSpace(_metadata.ReturnType) ? "Void" : _metadata.ReturnType;
                builder.Append($" : {returnType}");
            }

            builder.AppendLine();
        }

        private string GetBlockKeyword()
        {
            return _blockType switch
            {
                SimaticSdBlockType.Function => "FUNCTION",
                SimaticSdBlockType.DataBlock => "DATA_BLOCK",
                _ => "FUNCTION_BLOCK"
            };
        }

        private void AppendDeclarationSection(
            StringBuilder builder,
            string sectionKeyword,
            IReadOnlyList<DeclarationEntry> declarations,
            bool emitTrailingInitializer = false,
            bool suppressInlineDefaults = false)
        {
            if (declarations.Count == 0)
            {
                return;
            }

            builder.AppendLine($"    {sectionKeyword} ");
            foreach (var entry in declarations)
            {
                RenderDeclarationEntry(builder, entry, indentSpaces: 8, suppressInlineDefaults);
            }

            builder.AppendLine("    END_VAR");
            if (emitTrailingInitializer)
            {
                foreach (var assignment in EnumerateDefaults(declarations))
                {
                    builder.AppendLine($"    {assignment.Path} := {assignment.Value};");
                }

                AppendDataBlockInitializers(builder);
            }
        }

        private void RenderDeclarationEntry(StringBuilder builder, DeclarationEntry entry, int indentSpaces, bool suppressInlineDefaults)
        {
            var indent = new string(' ', indentSpaces);
            var identifier = FormatIdentifier(entry.Name);

            if (!string.IsNullOrWhiteSpace(entry.CommentId))
            {
                builder.AppendLine($"{indent}{{ S7_MLC := \"{entry.CommentId}\" }}");
            }

            RenderAttributes(builder, entry.Attributes, indentSpaces);

            if (entry.IsStruct)
            {
                builder.AppendLine($"{indent}{identifier} : STRUCT");
                foreach (var member in entry.Members)
                {
                    RenderDeclarationEntry(builder, member, indentSpaces + 4, suppressInlineDefaults);
                }

                builder.AppendLine($"{indent}END_STRUCT;");
                return;
            }

            var typeText = FormatTypeDescriptor(entry.Type);
            var line = $"{indent}{identifier} : {typeText}";
            if (!suppressInlineDefaults && !string.IsNullOrWhiteSpace(entry.DefaultValue))
            {
                line += $" := {entry.DefaultValue}";
            }

            builder.AppendLine(line + ";");
        }

        private void RenderAttributes(StringBuilder builder, IReadOnlyList<DeclarationAttribute>? attributes, int indentSpaces)
        {
            if (attributes == null || attributes.Count == 0)
            {
                return;
            }

            var indent = new string(' ', indentSpaces);
            foreach (var attribute in attributes)
            {
                builder.AppendLine($"{indent}{{ {attribute.Name} := \"{attribute.Value}\" }}");
            }
        }

        private IEnumerable<(string Path, string Value)> EnumerateDefaults(
            IReadOnlyList<DeclarationEntry> entries,
            string? parentPath = null)
        {
            foreach (var entry in entries)
            {
                var currentName = FormatIdentifier(entry.Name);
                var currentPath = string.IsNullOrEmpty(parentPath)
                    ? currentName
                    : $"{parentPath}.{currentName}";

                if (entry.IsStruct)
                {
                    foreach (var child in EnumerateDefaults(entry.Members, currentPath))
                    {
                        yield return child;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(entry.DefaultValue))
                {
                    yield return (currentPath, entry.DefaultValue!);
                }
            }
        }

        private void AppendDataBlockInitializers(StringBuilder builder)
        {
            if (_dataBlockInitializers.Count == 0)
            {
                return;
            }

            foreach (var initializer in _dataBlockInitializers)
            {
                if (string.IsNullOrWhiteSpace(initializer))
                {
                    continue;
                }

                builder.AppendLine($"    {initializer.Trim()}");
            }
        }

        private void AppendNetworks(StringBuilder builder)
        {
            if (_networks.Count == 0)
            {
                return;
            }

            builder.AppendLine();

            foreach (var network in _networks)
            {
                builder.AppendLine("    {");
                var language = network.Language
                               ?? _metadata.PreferredLanguage
                               ?? _metadata.EditorMode
                               ?? "FBD";
                builder.AppendLine($"        S7_Language := \"{language}\";");
                if (!string.IsNullOrWhiteSpace(network.TitleId))
                {
                    builder.AppendLine($"        S7_NetworkTitle := \"{network.TitleId}\";");
                }

                if (!string.IsNullOrWhiteSpace(network.CommentId))
                {
                    builder.AppendLine($"        S7_NetworkComment := \"{network.CommentId}\";");
                }

                builder.AppendLine("    }");

                var lines = network.Body.Trim('\r', '\n').Split(["\r\n", "\n"], StringSplitOptions.None);
                foreach (var line in lines)
                {
                    builder.AppendLine("    " + line);
                }
            }
        }

        private void AppendRawDeclarationSections(StringBuilder builder)
        {
            if (_rawDeclarationSections.Count == 0)
            {
                return;
            }

            foreach (var section in _rawDeclarationSections)
            {
                if (string.IsNullOrWhiteSpace(section))
                {
                    continue;
                }

                foreach (var line in NormalizeSection(section))
                {
                    builder.AppendLine(line);
                }
            }
        }

        private static string[] NormalizeSection(string text)
        {
            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim('\n');
            return normalized.Split('\n');
        }
    }

    internal enum SimaticSdBlockType
    {
        FunctionBlock,
        Function,
        DataBlock
    }
}
