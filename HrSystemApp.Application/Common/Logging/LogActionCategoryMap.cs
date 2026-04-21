using System.Reflection;

namespace HrSystemApp.Application.Common.Logging;

internal static class LogActionCategoryMap
{
    private static readonly Dictionary<string, LogCategory> _map = BuildFromNestedClasses();

    public static LogCategory GetCategory(string action) =>
        _map.GetValueOrDefault(action, LogCategory.Default);

    private static Dictionary<string, LogCategory> BuildFromNestedClasses()
    {
        var map = new Dictionary<string, LogCategory>(StringComparer.Ordinal);

        foreach (var nested in typeof(LogAction).GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            if (!Enum.TryParse<LogCategory>(nested.Name, ignoreCase: true, out var category))
                continue;

            foreach (var field in nested.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                if (field.IsLiteral && !field.IsInitOnly && field.GetValue(null) is string actionName)
                    map[actionName] = category;
            }
        }

        return map;
    }
}
