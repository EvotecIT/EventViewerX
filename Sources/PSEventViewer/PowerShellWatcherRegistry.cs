using EventViewerX;
using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using System.Threading;

namespace PSEventViewer;

/// <summary>Tracks only watchers created by the PowerShell module so module removal does not affect other hosts.</summary>
internal static class PowerShellWatcherRegistry {
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>> OwnedWatcherIds = new();
    private static readonly ConditionalWeakTable<PSModuleInfo, OwnerRegistration> ModuleOwners = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, long>> RunspaceOwners = new();
    private static readonly ConcurrentDictionary<string, Guid> StableModuleOwners = new(StringComparer.OrdinalIgnoreCase);
    private static long _ownerSequence;

    internal static Guid BeginModuleInstance(Guid runspaceId) {
        Guid ownerId = Guid.NewGuid();
        ConcurrentDictionary<Guid, long> owners = RunspaceOwners.GetOrAdd(
            runspaceId,
            static _ => new ConcurrentDictionary<Guid, long>());
        owners[ownerId] = Interlocked.Increment(ref _ownerSequence);
        return ownerId;
    }

    internal static Guid GetOwnerId(PSModuleInfo? module, Guid standaloneOwnerId) {
        if (module != null && ModuleOwners.TryGetValue(module, out OwnerRegistration? registeredOwner)) {
            if (IsActiveOwner(standaloneOwnerId, registeredOwner.OwnerId)) {
                return registeredOwner.OwnerId;
            }
            ModuleOwners.Remove(module);
        }

        string? moduleKey = GetStableModuleKey(module, standaloneOwnerId);
        if (moduleKey != null && StableModuleOwners.TryGetValue(moduleKey, out Guid stableOwnerId)) {
            if (IsActiveOwner(standaloneOwnerId, stableOwnerId)) {
                return RegisterModuleOwner(module!, standaloneOwnerId, stableOwnerId);
            }
            StableModuleOwners.TryRemove(moduleKey, out _);
        }

        if (moduleKey != null) {
            Guid proposedOwnerId = BeginModuleInstance(standaloneOwnerId);
            Guid ownerId = StableModuleOwners.GetOrAdd(moduleKey, proposedOwnerId);
            if (ownerId != proposedOwnerId) {
                StopAndRemoveOwner(standaloneOwnerId, proposedOwnerId);
            }
            return RegisterModuleOwner(module!, standaloneOwnerId, ownerId);
        }

        return standaloneOwnerId;
    }

    internal static void Register(Guid ownerId, Guid watcherId) {
        ConcurrentDictionary<Guid, byte> ownerWatchers = OwnedWatcherIds.GetOrAdd(
            ownerId,
            static _ => new ConcurrentDictionary<Guid, byte>());
        ownerWatchers.TryAdd(watcherId, 0);
    }

    internal static void Unregister(Guid ownerId, Guid watcherId) {
        if (!OwnedWatcherIds.TryGetValue(ownerId, out ConcurrentDictionary<Guid, byte>? ownerWatchers)) {
            return;
        }

        ownerWatchers.TryRemove(watcherId, out _);
    }

    internal static void StopAllOwned(Guid ownerId) {
        if (!OwnedWatcherIds.TryRemove(ownerId, out ConcurrentDictionary<Guid, byte>? ownerWatchers)) {
            return;
        }

        foreach (Guid watcherId in ownerWatchers.Keys) {
            WatcherManager.StopWatcher(watcherId);
        }
    }

    internal static void EndModuleInstance(Guid runspaceId, PSModuleInfo? module) {
        List<PSModuleInfo> moduleCandidates = GetModuleCandidates(module).ToList();
        Guid? ownerId = null;
        Guid effectiveRunspaceId = runspaceId;
        foreach (PSModuleInfo candidate in moduleCandidates) {
            if (ModuleOwners.TryGetValue(candidate, out OwnerRegistration? registeredOwner) &&
                TryGetOwnerRunspace(registeredOwner.OwnerId, out effectiveRunspaceId)) {
                ownerId = registeredOwner.OwnerId;
                break;
            }
        }
        if (!ownerId.HasValue) {
            return;
        }

        foreach (PSModuleInfo candidate in moduleCandidates) {
            ModuleOwners.Remove(candidate);
            string? candidateKey = GetStableModuleKey(candidate, effectiveRunspaceId);
            if (candidateKey != null) {
                StableModuleOwners.TryRemove(candidateKey, out _);
            }
        }
        StopAndRemoveOwner(effectiveRunspaceId, ownerId.Value);
    }

    private static Guid RegisterModuleOwner(PSModuleInfo module, Guid runspaceId, Guid ownerId) {
        OwnerRegistration registration = ModuleOwners.GetValue(module, _ => new OwnerRegistration(ownerId));
        lock (registration) {
            if (!registration.CleanupRegistered) {
                PowerShellModuleCleanup.Register(module, runspaceId, registration.OwnerId);
                registration.CleanupRegistered = true;
            }
        }
        return registration.OwnerId;
    }

    private static bool IsActiveOwner(Guid runspaceId, Guid ownerId)
        => RunspaceOwners.TryGetValue(runspaceId, out ConcurrentDictionary<Guid, long>? owners) &&
           owners.ContainsKey(ownerId);

    private static bool TryGetOwnerRunspace(Guid ownerId, out Guid runspaceId) {
        foreach (KeyValuePair<Guid, ConcurrentDictionary<Guid, long>> runspace in RunspaceOwners) {
            if (runspace.Value.ContainsKey(ownerId)) {
                runspaceId = runspace.Key;
                return true;
            }
        }
        runspaceId = Guid.Empty;
        return false;
    }

    internal static void StopAndRemoveOwner(Guid runspaceId, Guid ownerId) {
        StopAllOwned(ownerId);
        foreach (KeyValuePair<string, Guid> stableOwner in StableModuleOwners) {
            if (stableOwner.Value == ownerId) {
                StableModuleOwners.TryRemove(stableOwner.Key, out _);
            }
        }
        if (!RunspaceOwners.TryGetValue(runspaceId, out ConcurrentDictionary<Guid, long>? owners)) {
            return;
        }

        owners.TryRemove(ownerId, out _);
        if (owners.IsEmpty) {
            RunspaceOwners.TryRemove(runspaceId, out _);
        }
    }

    private static string? GetStableModuleKey(PSModuleInfo? module, Guid runspaceId) {
        if (module == null) {
            return null;
        }

        return $"{runspaceId:N}{GetStableModuleSuffix(module)}";
    }

    private static string GetStableModuleSuffix(PSModuleInfo module) {
        string identity = string.IsNullOrWhiteSpace(module.Path) ? module.Name : module.Path;
        return $"|{identity}|{module.Prefix}";
    }

    private static IEnumerable<PSModuleInfo> GetModuleCandidates(PSModuleInfo? module) {
        if (module == null) {
            yield break;
        }

        var pending = new Stack<PSModuleInfo>();
        var seen = new HashSet<PSModuleInfo>(ReferenceEqualityComparer<PSModuleInfo>.Instance);
        pending.Push(module);
        while (pending.Count > 0) {
            PSModuleInfo candidate = pending.Pop();
            if (!seen.Add(candidate)) {
                continue;
            }
            yield return candidate;
            foreach (PSModuleInfo nested in candidate.NestedModules) {
                pending.Push(nested);
            }
        }
    }

    private sealed class OwnerRegistration {
        internal OwnerRegistration(Guid ownerId) {
            OwnerId = ownerId;
        }

        internal Guid OwnerId { get; }
        internal bool CleanupRegistered { get; set; }
    }

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class {
        internal static readonly ReferenceEqualityComparer<T> Instance = new();

        public bool Equals(T? left, T? right)
            => ReferenceEquals(left, right);

        public int GetHashCode(T value)
            => RuntimeHelpers.GetHashCode(value);
    }
}
