using System.Text;
using static OpennessCopy.Services.CodeBuilders.SimaticSd.IdentifierFormattingUtils;

#nullable enable

namespace OpennessCopy.Services.CodeBuilders.SimaticSd;

public sealed partial class SimaticSdBuilder
{
    public sealed class SimaticSdInstanceDataBlockBuilder(
        string name,
        int? blockNumber,
        string instanceType,
        bool optimized,
        bool standardRetain,
        string version)
    {
        public SimaticSdInstanceDataBlock Build()
        {
            return new SimaticSdInstanceDataBlock(name, blockNumber, instanceType, optimized, standardRetain, version);
        }

        public SimaticSdTextResource BuildTextResource()
        {
            return new SimaticSdTextResource([]);
        }
    }

    public sealed class SimaticSdInstanceDataBlock
    {
        private readonly string _name;
        private readonly int? _blockNumber;
        private readonly string _instanceType;
        private readonly bool _optimized;
        private readonly bool _standardRetain;
        private readonly string _version;

        internal SimaticSdInstanceDataBlock(
            string name,
            int? blockNumber,
            string instanceType,
            bool optimized,
            bool standardRetain,
            string version)
        {
            _name = name;
            _blockNumber = blockNumber;
            _instanceType = FormatIdentifier(instanceType);
            _optimized = optimized;
            _standardRetain = standardRetain;
            _version = version;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine("{");
            if (_blockNumber.HasValue)
            {
                builder.AppendLine($"    S7_BlockNumber := \"{_blockNumber.Value}\";");
            }
            builder.AppendLine($"    S7_Optimized := \"{(_optimized ? "TRUE" : "FALSE")}\";");
            builder.AppendLine($"    S7_StandardRetain := \"{(_standardRetain ? "TRUE" : "FALSE")}\";");
            builder.AppendLine($"    S7_Version := \"{_version}\"");
            builder.AppendLine("}");
            builder.AppendLine($"DATA_BLOCK \"{_name}\" : {_instanceType}");
            builder.AppendLine("END_DATA_BLOCK");
            return builder.ToString();
        }
    }
}
