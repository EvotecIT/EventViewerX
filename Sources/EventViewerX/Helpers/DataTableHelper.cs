using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace EventViewerX.Helpers;

/// <summary>
/// Helper methods for converting collections of events to <see cref="DataTable"/>.
/// </summary>
public static class DataTableHelper {
    private sealed class MemberCache {
        public List<PropertyInfo> Properties { get; }
        public List<FieldInfo> Fields { get; }
        public PropertyInfo? DataProperty { get; }

        public MemberCache(List<PropertyInfo> properties, List<FieldInfo> fields, PropertyInfo? dataProperty) {
            Properties = properties;
            Fields = fields;
            DataProperty = dataProperty;
        }
    }

    private static readonly ConcurrentDictionary<Type, MemberCache> _cache = new();
    /// <summary>
    /// Converts a collection of <see cref="EventObject"/> instances to a <see cref="DataTable"/>.
    /// </summary>
    /// <param name="events">Events to convert.</param>
    /// <returns>DataTable containing event data.</returns>
    public static DataTable ToDataTable(this IEnumerable<EventObject> events) {
        return ToDataTableInternal(events);
    }

    /// <summary>
    /// Converts a collection of <see cref="EventTypeRecord"/> instances to a <see cref="DataTable"/>.
    /// </summary>
    /// <param name="events">Events to convert.</param>
    /// <returns>DataTable containing event data.</returns>
    public static DataTable ToDataTable(this IEnumerable<EventTypeRecord> events) {
        return ToDataTableInternal(events);
    }

    private static DataTable ToDataTableInternal<T>(IEnumerable<T> items) where T : class {
        if (items == null) throw new ArgumentNullException(nameof(items));
        var list = items.ToList();
        var dataTable = new DataTable(typeof(T).Name);
        if (!list.Any()) return dataTable;

        var cache = _cache.GetOrAdd(
            typeof(T),
            static type => new MemberCache(
                type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => IsSimpleType(p.PropertyType)).ToList(),
                type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Where(f => IsSimpleType(f.FieldType)).ToList(),
                type.GetProperty("Data", BindingFlags.Instance | BindingFlags.Public)
            ));

        var properties = cache.Properties;
        var fields = cache.Fields;
        var dataProperty = cache.DataProperty;

        foreach (var prop in properties) {
            var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            dataTable.Columns.Add(prop.Name, type);
        }
        foreach (var field in fields) {
            var type = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
            if (!dataTable.Columns.Contains(field.Name)) {
                dataTable.Columns.Add(field.Name, type);
            }
        }

        HashSet<string> dataKeys = new();
        if (dataProperty != null && dataProperty.PropertyType == typeof(Dictionary<string, string>)) {
            foreach (var item in list) {
                if (dataProperty.GetValue(item) is Dictionary<string, string> dict) {
                    foreach (var key in dict.Keys) {
                        dataKeys.Add(key);
                    }
                }
            }
            foreach (var key in dataKeys) {
                if (!dataTable.Columns.Contains(key)) {
                    dataTable.Columns.Add(key, typeof(string));
                }
            }
        }

        foreach (var item in list) {
            var row = dataTable.NewRow();
            foreach (var prop in properties) {
                row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
            }
            foreach (var field in fields) {
                row[field.Name] = field.GetValue(item) ?? DBNull.Value;
            }
            if (dataProperty != null && dataProperty.GetValue(item) is Dictionary<string, string> dict) {
                foreach (var key in dataKeys) {
                    if (dict.TryGetValue(key, out var value) && value != null) {
                        row[key] = value;
                    } else {
                        row[key] = DBNull.Value;
                    }
                }
            }
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }
  
    private static bool IsSimpleType(Type type) {
        var targetType = Nullable.GetUnderlyingType(type) ?? type;

        return targetType.IsPrimitive
            || targetType.IsEnum
            || targetType == typeof(string)
            || targetType == typeof(DateTime)
            || targetType == typeof(decimal)
            || targetType == typeof(Guid);
    }
    
}
