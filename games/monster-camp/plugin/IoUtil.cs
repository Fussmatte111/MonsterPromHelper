namespace MonsterCampHelper.Ingame;

/// <summary>Path helpers without .NET 4+ Path.Combine overloads (Unity Mono / CLR 2).</summary>
internal static class IoUtil
{
    public static string Combine(string part1, string part2)
    {
        if (string.IsNullOrEmpty(part1))
            return part2 ?? "";
        if (string.IsNullOrEmpty(part2))
            return part1;

        var end = part1[part1.Length - 1];
        if (end == '\\' || end == '/')
            return part1 + part2;

        var start = part2[0];
        if (start == '\\' || start == '/')
            return part1 + part2;

        return part1 + "\\" + part2;
    }

    public static string GetDirectoryName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "";

        var slash = path.LastIndexOf('\\');
        var alt = path.LastIndexOf('/');
        var idx = slash > alt ? slash : alt;
        return idx <= 0 ? "" : path.Substring(0, idx);
    }
}
