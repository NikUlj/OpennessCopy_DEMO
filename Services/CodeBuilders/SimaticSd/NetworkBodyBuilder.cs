#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpennessCopy.Services.CodeBuilders.SimaticSd;

/// <summary>
/// Helper that renders SIMATIC SD network bodies (NETWORK/RUNG sections) with consistent indentation.
/// </summary>
public sealed class NetworkBodyBuilder
{
    private const string StatementIndent = "    ";
    private const string RungIndent = "    ";
    private const string RungBodyIndent = "        ";

    private readonly StringBuilder _builder = new();
    private bool _networkStarted;
    private bool _networkEnded;
    private bool _hasContent;

    public static NetworkBodyBuilder Create() => new();

    public NetworkBodyBuilder AddStatement(string line)
    {
        if (line == null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        EnsureNetworkStarted();
        AppendIndentedLine(line, StatementIndent);
        _hasContent = true;
        return this;
    }

    public NetworkBodyBuilder AddStatements(params string[] lines)
    {
        if (lines == null)
        {
            throw new ArgumentNullException(nameof(lines));
        }

        foreach (var line in lines)
        {
            AddStatement(line);
        }

        return this;
    }

    public NetworkBodyBuilder AddBlock(string block)
    {
        if (block == null)
        {
            throw new ArgumentNullException(nameof(block));
        }

        EnsureNetworkStarted();
        foreach (var line in NormalizeBlock(block))
        {
            AppendIndentedLine(line, StatementIndent);
        }

        _hasContent = true;
        return this;
    }

    public NetworkBodyBuilder AddBlankLine()
    {
        EnsureNetworkStarted();
        _builder.AppendLine();
        _hasContent = true;
        return this;
    }

    public NetworkBodyBuilder AddRung(string? header, Action<RungBuilder>? configure = null, string? trailing = null)
    {
        EnsureNetworkStarted();
        AppendRungHeader(header);

        if (configure != null)
        {
            var rungBuilder = new RungBuilder(_builder);
            configure(rungBuilder);
        }

        _builder.Append(RungIndent);
        _builder.Append("END_RUNG");
        if (!string.IsNullOrWhiteSpace(trailing))
        {
            _builder.Append(' ');
            _builder.Append(trailing);
        }

        _builder.AppendLine();
        _hasContent = true;
        return this;
    }

    public NetworkBodyBuilder AddAndRung(IEnumerable<string> contacts, Action<RungBuilder>? configure = null, string? trailing = null)
    {
        if (contacts == null)
        {
            throw new ArgumentNullException(nameof(contacts));
        }

        var contactList = contacts.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        if (contactList.Count == 0)
        {
            throw new ArgumentException("At least one contact must be provided.", nameof(contacts));
        }

        var header = contactList[0];
        var remainder = contactList.Skip(1).ToArray();
        return AddRung(header, rung =>
        {
            rung.AppendAnd(remainder);
            configure?.Invoke(rung);
        }, trailing);
    }

    public NetworkBodyBuilder AddAndRung(params string[] contacts)
    {
        return AddAndRung((IEnumerable<string>?)contacts ?? [], null, null);
    }

    public NetworkBodyBuilder AddAndRung(Action<RungBuilder>? configure, params string[] contacts)
    {
        return AddAndRung((IEnumerable<string>?)contacts ?? [], configure, null);
    }

    public NetworkBodyBuilder AddOrRung(IEnumerable<string> contacts, Action<RungBuilder>? configure = null, string? trailing = null)
    {
        if (contacts == null)
        {
            throw new ArgumentNullException(nameof(contacts));
        }

        var contactList = contacts.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        if (contactList.Count == 0)
        {
            throw new ArgumentException("At least one contact must be provided.", nameof(contacts));
        }

        var header = contactList[0];
        var remainder = contactList.Skip(1).ToArray();
        return AddRung(header, rung =>
        {
            rung.AppendOr(remainder);
            configure?.Invoke(rung);
        }, trailing);
    }

    public NetworkBodyBuilder AddOrRung(params string[] contacts)
    {
        return AddOrRung((IEnumerable<string>?)contacts ?? [], null, null);
    }

    public NetworkBodyBuilder AddOrRung(Action<RungBuilder>? configure, params string[] contacts)
    {
        return AddOrRung((IEnumerable<string>?)contacts ?? [], configure, null);
    }

    public NetworkBodyBuilder AddRung(string? header, string body, string? trailing = null)
    {
        if (body == null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        return AddRung(header, rung => rung.AppendBlock(body), trailing);
    }

    public string Build()
    {
        if (!_networkStarted || !_hasContent)
        {
            throw new InvalidOperationException("A network requires at least one statement or rung.");
        }

        if (!_networkEnded)
        {
            _builder.AppendLine("END_NETWORK");
            _networkEnded = true;
        }

        return _builder.ToString();
    }

    private void EnsureNetworkStarted()
    {
        if (_networkStarted)
        {
            return;
        }

        _builder.AppendLine("NETWORK");
        _networkStarted = true;
    }

    private void AppendIndentedLine(string line, string indent)
    {
        _builder.Append(indent);
        _builder.AppendLine(line);
    }

    private void AppendRungHeader(string? header)
    {
        _builder.Append(RungIndent);
        _builder.Append("RUNG");
        if (!string.IsNullOrWhiteSpace(header))
        {
            _builder.Append(' ');
            _builder.Append(header);
        }

        _builder.AppendLine();
    }

    private static string[] NormalizeBlock(string block)
    {
        var normalized = block.Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim('\n', '\r');
        return normalized.Split(['\n'], StringSplitOptions.None);
    }

    public sealed class RungBuilder
    {
        private readonly StringBuilder _builder;

        internal RungBuilder(StringBuilder builder)
        {
            _builder = builder;
        }

        public RungBuilder AppendAnd(params string[] inputs)
        {
            return AppendAnd((IEnumerable<string>)inputs);
        }

        public RungBuilder AppendAnd(IEnumerable<string> inputs)
        {
            var list = NormalizeInputs(inputs);
            if (list.Count == 0)
            {
                _builder.AppendLine($"{RungBodyIndent}A()");
                return this;
            }

            if (list.Count == 1)
            {
                _builder.AppendLine($"{RungBodyIndent}A( in2 := {list[0]} )");
                return this;
            }

            AppendLogicBlock("A", list);
            return this;
        }

        public RungBuilder AppendOr(params string[] inputs)
        {
            return AppendOr((IEnumerable<string>)inputs);
        }

        public RungBuilder AppendOr(IEnumerable<string> inputs)
        {
            var list = NormalizeInputs(inputs);
            if (list.Count == 0)
            {
                _builder.AppendLine($"{RungBodyIndent}A()");
                return this;
            }

            if (list.Count == 1)
            {
                _builder.AppendLine($"{RungBodyIndent}O( in2 := {list[0]} )");
                return this;
            }

            AppendLogicBlock("O", list);
            return this;
        }

        public RungBuilder AppendMove(string input, params string[] outputs)
        {
            return AppendMove(input, (IEnumerable<string>)outputs);
        }

        public RungBuilder AppendMove(string input, IEnumerable<string> outputs)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Move input must be provided.", nameof(input));
            }

            var outList = NormalizeInputs(outputs);
            if (outList.Count == 0)
            {
                throw new ArgumentException("Move requires at least one output.", nameof(outputs));
            }

            _builder.AppendLine($"{RungBodyIndent}Move(");
            _builder.AppendLine($"{RungBodyIndent}    in := {input},");

            for (var i = 0; i < outList.Count; i++)
            {
                var suffix = i == outList.Count - 1 ? string.Empty : ",";
                _builder.AppendLine($"{RungBodyIndent}    out{i + 1} => {outList[i]}{suffix}");
            }

            _builder.AppendLine($"{RungBodyIndent})");
            return this;
        }

        public RungBuilder AppendLine(string line)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            _builder.Append(RungBodyIndent);
            _builder.AppendLine(line);
            return this;
        }

        private static List<string> NormalizeInputs(IEnumerable<string>? inputs)
        {
            var list = new List<string>();
            if (inputs == null)
            {
                return list;
            }

            foreach (var input in inputs)
            {
                if (!string.IsNullOrWhiteSpace(input))
                {
                    list.Add(input);
                }
            }

            return list;
        }

        private void AppendLogicBlock(string functionName, IReadOnlyList<string> inputs)
        {
            _builder.AppendLine($"{RungBodyIndent}{functionName}(");
            for (var i = 0; i < inputs.Count; i++)
            {
                var suffix = i == inputs.Count - 1 ? string.Empty : ",";
                _builder.AppendLine($"{RungBodyIndent}    in{i + 2} := {inputs[i]}{suffix}");
            }

            _builder.AppendLine($"{RungBodyIndent})");
        }

        public RungBuilder AppendBlock(string block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            var lines = NormalizeBlock(block);
            foreach (var line in lines)
            {
                AppendLine(line);
            }

            return this;
        }

        public RungBuilder AppendEmptyLine()
        {
            _builder.AppendLine();
            return this;
        }
    }
}
