using System;
using System.Reflection;

namespace MonsterPromHelper.Ingame;

/// <summary>Helpers safe on Unity Mono / CLR 2 (no Type.operator==).</summary>
internal static class MonoUtil
{
    public static bool IsNull(object? value) => ReferenceEquals(value, null);

    /// <summary>CLR 2 / Unity Mono has no string.IsNullOrWhiteSpace.</summary>
    public static bool IsNullOrWhiteSpace(string? value)
    {
        if (value == null)
            return true;
        for (var i = 0; i < value.Length; i++)
        {
            if (!char.IsWhiteSpace(value[i]))
                return false;
        }
        return true;
    }

    public static bool HasText(string? value) => !IsNullOrWhiteSpace(value);

    /// <summary>CLR 2 has no string.TrimEnd() without arguments (uses Array.Empty).</summary>
    public static string TrimEndWhitespace(string? value)
    {
        if (value == null || value.Length == 0)
            return value ?? "";

        var end = value.Length;
        while (end > 0 && char.IsWhiteSpace(value[end - 1]))
            end--;

        return end == value.Length ? value : value.Substring(0, end);
    }

    public static Type? FindGameType(string typeName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            var t = assemblies[i].GetType(typeName);
            if (!IsNull(t))
                return t;
        }

        return Type.GetType(typeName + ", Assembly-CSharp");
    }

    public static MethodInfo? FindMethod(Type type, string name)
    {
        return type.GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
