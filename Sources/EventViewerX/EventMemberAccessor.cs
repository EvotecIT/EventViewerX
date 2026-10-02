using System.Linq.Expressions;
using System.Reflection;

namespace EventViewerX;

/// <summary>Compiles getters once for reused predicate, observation, and report plans.</summary>
internal static class EventMemberAccessor {
    internal static Func<T, object?> CreateGetter<T>(MemberInfo member) {
        ParameterExpression instance = Expression.Parameter(typeof(T), "instance");
        UnaryExpression typed = Expression.Convert(instance, member.DeclaringType!);
        Expression access = member is PropertyInfo property
            ? Expression.Property(typed, property)
            : Expression.Field(typed, (FieldInfo)member);
        var getter = Expression.Lambda<Func<T, object?>>(
            Expression.Convert(access, typeof(object)), instance);
#if NET8_0_OR_GREATER
        return getter.Compile(preferInterpretation: !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
#else
        return getter.Compile();
#endif
    }
}
