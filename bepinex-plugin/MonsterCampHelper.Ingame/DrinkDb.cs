using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace MonsterCampHelper.Ingame;

public sealed class DrinkRecord
{
    public string Key = "";
    public string Name = "";
    public string Effect = "";
    public string Misc = "";
}

public sealed class DrinkDb
{
    private readonly Dictionary<string, DrinkRecord> _drinks =
        new Dictionary<string, DrinkRecord>(StringComparer.OrdinalIgnoreCase);
    private int _uniqueCount;

    public int DrinkCount => _uniqueCount;

    public void LoadFromFolder(string dataFolder)
    {
        _drinks.Clear();
        _uniqueCount = 0;
        var path = IoUtil.Combine(dataFolder, "drinks_db.json");
        if (!File.Exists(path))
            return;
        Load(File.ReadAllText(path));
    }

    private void Load(string json)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match block in Regex.Matches(json, "\"([^\"]+)\"\\s*:\\s*\\{"))
        {
            var key = block.Groups[1].Value;
            var start = block.Index;
            var slice = json.Substring(start, Math.Min(1200, json.Length - start));
            var rec = new DrinkRecord
            {
                Key = key,
                Name = MatchStr(slice, "name") ?? key,
                Effect = MatchStr(slice, "effect") ?? "",
                Misc = MatchStr(slice, "misc") ?? "",
            };
            if (!MonoUtil.HasText(rec.Name))
                continue;
            _drinks[rec.Name] = rec;
            _drinks[rec.Key] = rec;
            if (seen.Add(rec.Name))
                _uniqueCount++;
        }
    }

    public DrinkRecord? Get(string? name)
    {
        if (MonoUtil.IsNullOrWhiteSpace(name))
            return null;
        var key = name.Trim();
        if (_drinks.TryGetValue(key, out var exact))
            return exact;

        foreach (var kv in _drinks)
        {
            if (string.Equals(kv.Value.Name, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        return null;
    }

    private static string? MatchStr(string slice, string key)
    {
        var m = Regex.Match(slice, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : null;
    }
}
