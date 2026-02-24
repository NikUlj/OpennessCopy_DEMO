using System;
using System.Collections.Generic;

#nullable enable

namespace OpennessCopy.Services.CodeBuilders.SimaticSd;

/// <summary>
/// Ladder-oriented wrapper around NetworkBodyBuilder that auto-manages wire identifiers.
/// </summary>
public sealed class LadderNetworkBuilder
{
    private readonly NetworkBodyBuilder _inner;
    private readonly Dictionary<string, WireHandle> _wires = new(StringComparer.OrdinalIgnoreCase);
    private int _nextWireIndex = 1;
    private WireHandle _lastWire;
    private (string contact, WireHandle wire)? _pendingGuard;

    private LadderNetworkBuilder()
    {
        _inner = NetworkBodyBuilder.Create();
        _lastWire = PowerRail;
    }

    public static LadderNetworkBuilder Create() => new();

    public WireHandle PowerRail => GetOrCreateWire("wire#powerrail");

    public WireHandle NewWire()
    {
        var wireName = $"wire#w{_nextWireIndex++}";
        return GetOrCreateWire(wireName);
    }

    public WireHandle Wire(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Wire name must be provided.", nameof(name));
        }

        return GetOrCreateWire(name);
    }

    public LadderNetworkBuilder AddRung(Action<RungBuilder> configure)
    {
        return AddRung(PowerRail, configure);
    }

    public LadderNetworkBuilder AddRung(WireHandle start, Action<RungBuilder> configure, WireHandle? end = null)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var effectiveStart = start;
        WireHandle? effectiveEnd = end;

        if (_pendingGuard.HasValue && (start.Equals(PowerRail) || start.Equals(_pendingGuard.Value.wire)))
        {
            var guard = _pendingGuard.Value;
            _pendingGuard = null;
            _lastWire = start;
            var lastBefore = _lastWire;

            _inner.AddRung(PowerRail.Token, rung =>
            {
                rung.AppendLine($"Contact( {guard.contact} )");
                rung.AppendLine(guard.wire.Token);
                var wrapper = new RungBuilder(rung, this);
                configure(wrapper);
            }, effectiveEnd?.Token);

            if (effectiveEnd.HasValue)
            {
                _lastWire = effectiveEnd.Value;
            }

            if (_lastWire.Equals(lastBefore))
            {
                _lastWire = guard.wire;
            }

            return this;
        }

        _inner.AddRung(effectiveStart.Token, rung =>
        {
            var wrapper = new RungBuilder(rung, this);
            configure(wrapper);
        }, effectiveEnd?.Token);

        if (effectiveEnd.HasValue)
        {
            _lastWire = effectiveEnd.Value;
        }

        return this;
    }

    public LadderNetworkBuilder AddRungFromLast(Action<RungBuilder> configure, out WireHandle endWire)
    {
        endWire = NewWire();
        AddRung(_lastWire, configure, endWire);
        return this;
    }

    public LadderNetworkBuilder AddRungFromLast(Action<RungBuilder> configure)
    {
        return AddRung(_lastWire, configure, null);
    }

    /// <summary>
    /// Preloads a guard contact to be applied to the next rung added from powerrail.
    /// Returns the wire that the guard will hand off to.
    /// </summary>
    public WireHandle WithGuard(string guardContact)
    {
        if (string.IsNullOrWhiteSpace(guardContact))
        {
            throw new ArgumentException("Guard contact must be provided.", nameof(guardContact));
        }

        var wire = NewWire();
        _pendingGuard = (guardContact, wire);
        return wire;
    }

    /// <summary>
    /// Adds a single rung starting from powerrail that prepends a guard contact, inserts a new wire, and then executes the remaining configuration.
    /// Returns the inserted wire handle so callers can fan out subsequent rungs from it.
    /// </summary>
    public WireHandle AddGuardedRung(string guardContact, Action<RungBuilder> configure)
    {
        if (string.IsNullOrWhiteSpace(guardContact))
        {
            throw new ArgumentException("Guard contact must be provided.", nameof(guardContact));
        }

        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var branchWire = NewWire();
        AddRung(PowerRail, rung =>
        {
            rung.Contact(guardContact);
            rung.InsertWire(branchWire);
            configure(rung);
        }, branchWire);

        return branchWire;
    }

    /// <summary>
    /// Adds a simple guard rung (powerrail -> contact -> new wire) and returns the new wire handle.
    /// This is useful when you want to fan out multiple rungs behind a shared contact such as "M_TRUE".
    /// </summary>
    public WireHandle AddGuard(string contactExpression)
    {
        if (string.IsNullOrWhiteSpace(contactExpression))
        {
            throw new ArgumentException("Guard contact must be provided.", nameof(contactExpression));
        }

        var next = NewWire();
        AddRung(PowerRail, r =>
        {
            r.Contact(contactExpression);
            r.InsertWire(next);
        }, next);
        return next;
    }

    /// <summary>
    /// Returns the starting wire for a sequence of rungs. If <paramref name="guardContact"/> is provided,
    /// a guard rung is emitted (powerrail -> contact -> new wire) and that wire is returned; otherwise, powerrail is returned.
    /// </summary>
    public WireHandle GetStartWire(string? guardContact)
    {
        return string.IsNullOrWhiteSpace(guardContact)
            ? PowerRail
            : AddGuard(guardContact!);
    }

    public string Build()
    {
        return _inner.Build();
    }

    private WireHandle GetOrCreateWire(string name)
    {
        if (_wires.TryGetValue(name, out var handle))
        {
            return handle;
        }

        var token = name.StartsWith("wire#", StringComparison.OrdinalIgnoreCase)
            ? name
            : $"wire#{name}";

        handle = new WireHandle(token);
        _wires[name] = handle;
        return handle;
    }

    public readonly record struct WireHandle
    {
        internal WireHandle(string token)
        {
            Token = token;
        }

        internal string Token { get; }
    }

    public sealed class RungBuilder
    {
        private readonly NetworkBodyBuilder.RungBuilder _inner;
        private readonly LadderNetworkBuilder _owner;

        internal RungBuilder(NetworkBodyBuilder.RungBuilder inner, LadderNetworkBuilder owner)
        {
            _inner = inner;
            _owner = owner;
        }

        public RungBuilder Contact(string expression)
        {
            _inner.AppendLine($"Contact( {expression} )");
            return this;
        }
        
        // ReSharper disable once InconsistentNaming
        public RungBuilder IContact(string expression)
        {
            _inner.AppendLine($"I_Contact( {expression} )");
            return this;
        }

        public RungBuilder Not()
        {
            _inner.AppendLine("Not()");
            return this;
        }

        public RungBuilder Coil(string expression)
        {
            _inner.AppendLine($"Coil( {expression} )");
            return this;
        }

        public RungBuilder Move(string input, params string[] outputs)
        {
            _inner.AppendMove(input, outputs);
            return this;
        }

        public RungBuilder Move(string input, IEnumerable<string> outputs)
        {
            _inner.AppendMove(input, outputs);
            return this;
        }

        public RungBuilder Eq(string in1, string in2, string? attribute = null)
        {
            if (!string.IsNullOrWhiteSpace(attribute))
            {
                _inner.AppendLine(attribute!);
            }

            _inner.AppendLine("EQ_Contact(");
            _inner.AppendLine($"        in1 := {in1},");
            _inner.AppendLine($"        in2 := {in2}");
            _inner.AppendLine("    )");
            return this;
        }

        public WireHandle InsertWire()
        {
            var wire = _owner.NewWire();
            _inner.AppendLine(wire.Token);
            _owner._lastWire = wire;
            return wire;
        }

        public WireHandle InsertWire(WireHandle wire)
        {
            _inner.AppendLine(wire.Token);
            _owner._lastWire = wire;
            return wire;
        }
    }
}
