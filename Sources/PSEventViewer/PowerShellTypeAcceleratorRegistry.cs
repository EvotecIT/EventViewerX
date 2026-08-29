using System.Reflection;

namespace PSEventViewer;

/// <summary>Publishes isolated EventViewerX public types to PowerShell's type resolver for the module lifetime.</summary>
internal static class PowerShellTypeAcceleratorRegistry {
    private const BindingFlags StaticMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Type> OwnedAccelerators = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Type> DisplacedAccelerators = new(StringComparer.OrdinalIgnoreCase);
    private static int _registrations;

    internal static void Register() {
        lock (Sync) {
            if (_registrations > 0) {
                _registrations++;
                return;
            }

            Type acceleratorType = ResolveAcceleratorType();
            PropertyInfo getProperty = acceleratorType.GetProperty("Get", StaticMembers)
                ?? throw new InvalidOperationException("PowerShell type-accelerator catalog is unavailable.");
            MethodInfo addMethod = ResolveAddMethod(acceleratorType);
            MethodInfo removeMethod = ResolveRemoveMethod(acceleratorType);
            var catalog = (IDictionary)getProperty.GetValue(null, null)!;

            try {
                foreach (Type type in GetPublicContractTypes()) {
                    string? name = type.FullName;
                    if (string.IsNullOrWhiteSpace(name)) {
                        continue;
                    }

                    if (catalog[name] is Type existing) {
                        if (ReferenceEquals(existing, type)) {
                            continue;
                        }
                        DisplacedAccelerators[name] = existing;
                        removeMethod.Invoke(null, new object[] { name });
                    }

                    addMethod.Invoke(null, new object[] { name, type });
                    OwnedAccelerators[name] = type;
                }
                _registrations = 1;
            } catch {
                RollBackRegistration(catalog, addMethod, removeMethod);
                throw;
            }
        }
    }

    internal static void Unregister() {
        lock (Sync) {
            if (_registrations == 0 || --_registrations != 0) {
                return;
            }

            Type acceleratorType = ResolveAcceleratorType();
            PropertyInfo getProperty = acceleratorType.GetProperty("Get", StaticMembers)
                ?? throw new InvalidOperationException("PowerShell type-accelerator catalog is unavailable.");
            MethodInfo removeMethod = ResolveRemoveMethod(acceleratorType);
            MethodInfo addMethod = ResolveAddMethod(acceleratorType);
            var catalog = (IDictionary)getProperty.GetValue(null, null)!;
            RestoreAccelerators(catalog, addMethod, removeMethod);
        }
    }

    private static void RollBackRegistration(IDictionary catalog, MethodInfo addMethod, MethodInfo removeMethod) {
        KeyValuePair<string, Type>[] displacedSnapshot = DisplacedAccelerators.ToArray();
        RestoreAccelerators(catalog, addMethod, removeMethod);
        foreach (KeyValuePair<string, Type> displaced in displacedSnapshot) {
            if (catalog[displaced.Key] == null) {
                addMethod.Invoke(null, new object[] { displaced.Key, displaced.Value });
            }
        }
    }

    private static void RestoreAccelerators(IDictionary catalog, MethodInfo addMethod, MethodInfo removeMethod) {
        foreach (KeyValuePair<string, Type> accelerator in OwnedAccelerators.Reverse()) {
            if (catalog[accelerator.Key] is not Type current || !ReferenceEquals(current, accelerator.Value)) {
                continue;
            }

            removeMethod.Invoke(null, new object[] { accelerator.Key });
            if (DisplacedAccelerators.TryGetValue(accelerator.Key, out Type? displaced)) {
                addMethod.Invoke(null, new object[] { accelerator.Key, displaced });
            }
        }
        OwnedAccelerators.Clear();
        DisplacedAccelerators.Clear();
    }

    private static IEnumerable<Type> GetPublicContractTypes() {
        Assembly[] assemblies = {
            typeof(EventObject).Assembly,
            typeof(EventViewerX.Reporting.EventReportEmailRenderer).Assembly,
            typeof(EventViewerX.Storage.EventStore).Assembly,
            typeof(EventViewerX.Sigma.SigmaRuleCompiler).Assembly,
            typeof(EventViewerX.Evtx.EvtxSavedEventReader).Assembly
        };
        return assemblies
            .Distinct()
            .SelectMany(static assembly => assembly.GetExportedTypes())
            .OrderBy(static type => type.FullName, StringComparer.Ordinal);
    }

    private static Type ResolveAcceleratorType() =>
        typeof(PSObject).Assembly.GetType("System.Management.Automation.TypeAccelerators", throwOnError: true)!;

    private static MethodInfo ResolveAddMethod(Type acceleratorType) =>
        acceleratorType.GetMethod(
            "Add",
            StaticMembers,
            binder: null,
            new[] { typeof(string), typeof(Type) },
            modifiers: null)
        ?? throw new InvalidOperationException("PowerShell type-accelerator registration is unavailable.");

    private static MethodInfo ResolveRemoveMethod(Type acceleratorType) =>
        acceleratorType.GetMethod(
            "Remove",
            StaticMembers,
            binder: null,
            new[] { typeof(string) },
            modifiers: null)
        ?? throw new InvalidOperationException("PowerShell type-accelerator removal is unavailable.");
}
