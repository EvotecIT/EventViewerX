using System;
using System.Collections.Generic;

namespace EventViewerX;

/// <summary>
/// Maintains a bounded set of incomplete PowerShell script-block fragments and releases complete groups immediately.
/// </summary>
internal sealed class PowerShellScriptFragmentCache {
    private readonly int _maxPendingScripts;
    private readonly int _maxCachedEvents;
    private readonly Dictionary<string, PendingScript> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _pendingOrder = new();
    private int _cachedEventCount;
    private int _evictedScriptCount;
    private int _evictedEventCount;
    private long _nextEncounterOrder;

    internal PowerShellScriptFragmentCache(int maxPendingScripts, int maxCachedEvents) {
        if (maxPendingScripts <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxPendingScripts));
        }
        if (maxCachedEvents <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxCachedEvents));
        }

        _maxPendingScripts = maxPendingScripts;
        _maxCachedEvents = maxCachedEvents;
    }

    internal int PendingScriptCount => _pending.Count;

    internal int CachedEventCount => _cachedEventCount;

    internal int EvictedScriptCount => _evictedScriptCount;

    internal int EvictedEventCount => _evictedEventCount;

    internal long? NewestPendingEncounterOrder => _pendingOrder.First == null
        ? null
        : _pending[_pendingOrder.First.Value].EncounterOrder;

    internal bool Contains(string scriptBlockId) => _pending.ContainsKey(scriptBlockId);

    internal bool TryAdd(
        string scriptBlockId,
        int messageNumber,
        int messageTotal,
        string scriptText,
        EventObject eventObject,
        out PowerShellScriptAssembly? completed) {

        if (messageNumber < 0 || messageNumber > PowerShellEventEngine.MaximumPowerShellScriptPartCount) {
            throw new ArgumentOutOfRangeException(nameof(messageNumber));
        }
        if (messageTotal < 0 || messageTotal > PowerShellEventEngine.MaximumPowerShellScriptPartCount) {
            throw new ArgumentOutOfRangeException(nameof(messageTotal));
        }

        if (!_pending.TryGetValue(scriptBlockId, out PendingScript? script)) {
            LinkedListNode<string> orderNode = _pendingOrder.AddLast(scriptBlockId);
            script = new PendingScript(_nextEncounterOrder++, orderNode);
            _pending.Add(scriptBlockId, script);
        }

        script.Events.Add(eventObject);
        _cachedEventCount++;
        if (messageNumber == 0) {
            script.MetaRecord = eventObject;
        } else if (messageNumber > 0) {
            script.Parts[messageNumber] = scriptText;
        }
        if (messageTotal > 0) {
            script.ExpectedParts = Math.Max(script.ExpectedParts, messageTotal);
        }

        if (IsComplete(script)) {
            completed = Remove(scriptBlockId, script, isComplete: true);
            return true;
        }

        EnforceLimits();
        completed = null;
        return false;
    }

    internal IReadOnlyList<PowerShellScriptAssembly> Drain() {
        var assemblies = new List<PowerShellScriptAssembly>(_pending.Count);
        while (_pendingOrder.First != null) {
            string scriptBlockId = _pendingOrder.First.Value;
            PendingScript script = _pending[scriptBlockId];
            assemblies.Add(Remove(scriptBlockId, script, isComplete: IsComplete(script)));
        }

        return assemblies;
    }

    private bool IsComplete(PendingScript script) {
        if (script.ExpectedParts <= 0 || script.ExpectedParts > _maxCachedEvents || script.Parts.Count < script.ExpectedParts) {
            return false;
        }

        for (int partNumber = 1; partNumber <= script.ExpectedParts; partNumber++) {
            if (!script.Parts.ContainsKey(partNumber)) {
                return false;
            }
        }

        return true;
    }

    private void EnforceLimits() {
        while (_pending.Count > _maxPendingScripts || _cachedEventCount > _maxCachedEvents) {
            // Native input is read newest-to-oldest, so the last newly encountered group is the oldest.
            LinkedListNode<string>? oldest = _pendingOrder.Last;
            if (oldest == null) {
                break;
            }

            string scriptBlockId = oldest.Value;
            PendingScript script = _pending[scriptBlockId];
            _cachedEventCount -= script.Events.Count;
            _evictedScriptCount++;
            _evictedEventCount += script.Events.Count;
            _pending.Remove(scriptBlockId);
            _pendingOrder.Remove(oldest);
        }
    }

    private PowerShellScriptAssembly Remove(string scriptBlockId, PendingScript script, bool isComplete) {
        _pending.Remove(scriptBlockId);
        _pendingOrder.Remove(script.OrderNode);
        _cachedEventCount -= script.Events.Count;
        return new PowerShellScriptAssembly(
            scriptBlockId,
            script.MetaRecord,
            script.Events.AsReadOnly(),
            new Dictionary<int, string>(script.Parts),
            script.ExpectedParts,
            isComplete,
            script.EncounterOrder);
    }

    private sealed class PendingScript {
        internal PendingScript(long encounterOrder, LinkedListNode<string> orderNode) {
            EncounterOrder = encounterOrder;
            OrderNode = orderNode;
        }

        internal long EncounterOrder { get; }

        internal LinkedListNode<string> OrderNode { get; }

        internal EventObject? MetaRecord { get; set; }

        internal List<EventObject> Events { get; } = new();

        internal Dictionary<int, string> Parts { get; } = new();

        internal int ExpectedParts { get; set; }
    }
}

/// <summary>Represents one complete or bounded end-of-query PowerShell script-block assembly.</summary>
internal sealed class PowerShellScriptAssembly {
    internal PowerShellScriptAssembly(
        string scriptBlockId,
        EventObject? metaRecord,
        IReadOnlyList<EventObject> events,
        IReadOnlyDictionary<int, string> parts,
        int expectedParts,
        bool isComplete,
        long encounterOrder = 0) {

        ScriptBlockId = scriptBlockId;
        MetaRecord = metaRecord;
        Events = events;
        Parts = parts;
        ExpectedParts = expectedParts;
        IsComplete = isComplete;
        EncounterOrder = encounterOrder;
    }

    internal string ScriptBlockId { get; }

    internal EventObject? MetaRecord { get; }

    internal IReadOnlyList<EventObject> Events { get; }

    internal IReadOnlyDictionary<int, string> Parts { get; }

    internal int ExpectedParts { get; }

    internal bool IsComplete { get; }

    /// <summary>Zero-based native encounter order; lower values are newer in a reverse-directed query.</summary>
    internal long EncounterOrder { get; }
}
