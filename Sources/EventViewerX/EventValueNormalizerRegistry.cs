namespace EventViewerX;

/// <summary>Canonical registry for deterministic built-in event-value normalization.</summary>
public static class EventValueNormalizerRegistry {
    private static readonly IEventValueNormalizer[] Normalizers = {
        new DirectoryOperationValueNormalizer(),
        new UserAccountControlValueNormalizer(),
        new ActiveDirectoryGeneralizedTimeValueNormalizer(),
        new ActiveDirectoryFileTimeValueNormalizer(),
        new EventIdentityValueNormalizer(),
        new EventMultiValueNormalizer(),
        new AuditResourceValueNormalizer()
    };

    /// <summary>Returns the ordered built-in normalizers.</summary>
    public static IReadOnlyList<IEventValueNormalizer> GetNormalizers() => Normalizers.ToArray();

    /// <summary>Normalizes one field using the first context-specific owner or returns an unchanged value.</summary>
    public static EventNormalizedValue Normalize(EventValueContext context) {
        Validate(context);
        foreach (IEventValueNormalizer normalizer in Normalizers) {
            if (!normalizer.CanNormalize(context)) {
                continue;
            }
            EventNormalizedValue result = normalizer.Normalize(context) ??
                throw new InvalidOperationException($"Normalizer '{normalizer.Name}' returned null.");
            return result;
        }
        return EventValueNormalizer.Unchanged(context);
    }

    private static void Validate(EventValueContext context) {
        if (context == null) {
            throw new ArgumentNullException(nameof(context));
        }
        if (string.IsNullOrWhiteSpace(context.FieldName)) {
            throw new ArgumentException("FieldName cannot be empty.", nameof(context));
        }
        context.ProviderName = context.ProviderName?.Trim() ?? string.Empty;
        context.TypeName = context.TypeName?.Trim() ?? string.Empty;
        context.FieldName = context.FieldName.Trim();
        context.Values ??= new Dictionary<string, object?>();
    }
}
