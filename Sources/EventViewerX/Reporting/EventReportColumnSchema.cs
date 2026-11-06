using System.Reflection;

namespace EventViewerX.Reporting;

/// <summary>Serializable column contract used to persist and rehydrate a report section.</summary>
public sealed class EventReportColumnSchema {
    /// <summary>Stable field name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Human-friendly heading.</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Runtime-neutral CLR type identity when known.</summary>
    public string ValueTypeName { get; set; } = GetStableTypeName(typeof(object));
    /// <summary>Alternative field names accepted by typed predicate builders.</summary>
    public IReadOnlyList<string> Aliases { get; set; } = Array.Empty<string>();

    /// <summary>Creates a runtime-neutral identity for a CLR type.</summary>
    public static string GetStableTypeName(Type type) {
        if (type == null) {
            throw new ArgumentNullException(nameof(type));
        }
        if (type.IsArray) {
            return GetStableTypeName(type.GetElementType()!) + "[]";
        }
        if (type.IsGenericType) {
            string definition = type.GetGenericTypeDefinition().FullName ?? type.GetGenericTypeDefinition().Name;
            return definition + "[" + string.Join(",", type.GetGenericArguments().Select(GetStableTypeName)) + "]";
        }
        return type.FullName ?? type.Name;
    }

    internal static Type ResolveKnownValueTypeName(string? name) {
        string normalized = NormalizeKnownValueTypeName(name);
        return KnownValueTypes.TryGetValue(normalized, out Type? type) ? type : typeof(object);
    }

    internal static string NormalizeKnownValueTypeName(string? name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return "System.Object";
        }
        string trimmed = name!.Trim();
        string normalized = NormalizeTypeIdentity(trimmed);
        return KnownValueTypes.ContainsKey(normalized) ? normalized : trimmed;
    }

    private static string NormalizeTypeIdentity(string name) {
        name = name.Trim();
        if (name.StartsWith("[", StringComparison.Ordinal) && name.EndsWith("]", StringComparison.Ordinal)) {
            name = name.Substring(1, name.Length - 2);
        }
        int depth = 0;
        for (int index = 0; index < name.Length; index++) {
            if (name[index] == '[') { depth++; }
            if (name[index] == ']') { depth--; }
            if (name[index] == ',' && depth == 0) {
                name = name.Substring(0, index).Trim();
                break;
            }
        }
        int open = name.IndexOf('[');
        if (open < 0 || name.EndsWith("[]", StringComparison.Ordinal)) {
            return name;
        }
        string arguments = name.Substring(open + 1, name.Length - open - 2);
        return name.Substring(0, open) + "[" + string.Join(",",
            SplitGenericArguments(arguments).Select(NormalizeTypeIdentity)) + "]";
    }

    private static readonly IReadOnlyDictionary<string, Type> KnownValueTypes =
        EventJson.ValueTypes.Distinct().ToDictionary(GetStableTypeName, StringComparer.Ordinal);

    /// <summary>Resolves a runtime-neutral or legacy assembly-qualified type identity.</summary>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Resolving arbitrary persisted CLR type names requires preserved types. Use schema type names without rehydration in trimmed consumers.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary persisted generic and array types may not have Native AOT instantiations.")]
#endif
    public static Type ResolveValueTypeName(string? valueTypeName) {
        if (string.IsNullOrWhiteSpace(valueTypeName)) {
            return typeof(object);
        }
        string name = valueTypeName!.Trim();
        return TryResolveValueTypeName(name) ?? typeof(object);
    }

    /// <summary>Converts a runtime-neutral or legacy assembly-qualified identity into its stable form.</summary>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Resolving arbitrary persisted CLR type names requires preserved types. Use schema type names without rehydration in trimmed consumers.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary persisted generic and array types may not have Native AOT instantiations.")]
#endif
    public static string NormalizeValueTypeName(string? valueTypeName) {
        if (string.IsNullOrWhiteSpace(valueTypeName)) {
            return GetStableTypeName(typeof(object));
        }
        string name = valueTypeName!.Trim();
        Type? resolved = TryResolveValueTypeName(name);
        return resolved == null ? name : GetStableTypeName(resolved);
    }

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Resolving arbitrary persisted CLR type names requires preserved types. Use schema type names without rehydration in trimmed consumers.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary persisted generic and array types may not have Native AOT instantiations.")]
#endif
    private static Type? TryResolveValueTypeName(string name) =>
        Type.GetType(name, throwOnError: false) ??
        ResolveLegacyQualifiedType(name) ??
        ResolveStableType(name);

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Resolving arbitrary persisted CLR type names requires preserved types. Use schema type names without rehydration in trimmed consumers.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary persisted generic and array types may not have Native AOT instantiations.")]
#endif
    private static Type? ResolveLegacyQualifiedType(string name) {
        try {
            return Type.GetType(
                name,
                ResolveLegacyAssembly,
                ResolveLegacyType,
                throwOnError: false);
        } catch (FileLoadException) {
            return null;
        } catch (FileNotFoundException) {
            return null;
        } catch (TypeLoadException) {
            return null;
        }
    }

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Resolving arbitrary persisted CLR type names requires preserved types. Use schema type names without rehydration in trimmed consumers.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary persisted generic and array types may not have Native AOT instantiations.")]
#endif
    private static Assembly? ResolveLegacyAssembly(AssemblyName requested) {
        string name = requested.Name ?? string.Empty;
        if (name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("System.Private.CoreLib", StringComparison.OrdinalIgnoreCase)) {
            return typeof(object).Assembly;
        }
        Assembly? loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(
                assembly.GetName().Name,
                name,
                StringComparison.OrdinalIgnoreCase));
        if (loaded != null) {
            return loaded;
        }
        try {
            return string.IsNullOrWhiteSpace(name) ? null : Assembly.Load(new AssemblyName(name));
        } catch (FileLoadException) {
            return null;
        } catch (FileNotFoundException) {
            return null;
        }
    }

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Resolving arbitrary persisted CLR type names requires preserved types. Use schema type names without rehydration in trimmed consumers.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary persisted generic and array types may not have Native AOT instantiations.")]
#endif
    private static Type? ResolveLegacyType(Assembly? assembly, string typeName, bool ignoreCase) =>
        assembly?.GetType(typeName, throwOnError: false, ignoreCase: ignoreCase) ??
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(candidate => candidate.GetType(typeName, throwOnError: false, ignoreCase: ignoreCase))
            .FirstOrDefault(static type => type != null);

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Resolving arbitrary persisted CLR type names requires preserved types. Use schema type names without rehydration in trimmed consumers.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary persisted generic and array types may not have Native AOT instantiations.")]
#endif
    private static Type? ResolveStableType(string name) {
        if (name.EndsWith("[]", StringComparison.Ordinal)) {
            return ResolveStableType(name.Substring(0, name.Length - 2))?.MakeArrayType();
        }
        int open = name.IndexOf('[');
        if (open > 0 && name.EndsWith("]", StringComparison.Ordinal)) {
            Type? definition = ResolveNamedType(name.Substring(0, open));
            if (definition == null || !definition.IsGenericTypeDefinition) {
                return null;
            }
            string argumentsText = name.Substring(open + 1, name.Length - open - 2);
            Type?[] arguments = SplitGenericArguments(argumentsText)
                .Select(ResolveStableType)
                .ToArray();
            return arguments.Any(static argument => argument == null)
                ? null
                : definition.MakeGenericType(arguments.Cast<Type>().ToArray());
        }
        return ResolveNamedType(name);
    }

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Resolving arbitrary persisted CLR type names requires preserved types. Use schema type names without rehydration in trimmed consumers.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary persisted generic and array types may not have Native AOT instantiations.")]
#endif
    private static Type? ResolveNamedType(string name) {
        Type? resolved = Type.GetType(name, throwOnError: false);
        if (resolved != null) {
            return resolved;
        }
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            resolved = assembly.GetType(name, throwOnError: false, ignoreCase: false);
            if (resolved != null) {
                return resolved;
            }
        }
        return null;
    }

    private static IReadOnlyList<string> SplitGenericArguments(string value) {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for (int index = 0; index < value.Length; index++) {
            switch (value[index]) {
                case '[':
                    depth++;
                    break;
                case ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(value.Substring(start, index - start));
                    start = index + 1;
                    break;
            }
        }
        result.Add(value.Substring(start));
        return result;
    }
}
