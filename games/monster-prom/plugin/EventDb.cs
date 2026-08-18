using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace MonsterPromHelper.Ingame;

public sealed class EventRecord
{
    public string Key = "";
    public string Name = "";
    public string Route = "";
    public string Type = "";
    public List<OptionRecord> Options = new List<OptionRecord>();
}

public sealed class OptionRecord
{
    public int Option;
    public string Stat = "";
    public string Hint = "";
}

public sealed class EventDb
{
    public static readonly string[] StatKeys = { "SMARTS", "BOLD", "CREATIVE", "CHARM", "FUN", "MONEY" };

    private readonly Dictionary<string, EventRecord> _events = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _secretEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _secretEventToRoute = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _secretEventToWikiTitle = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _secretChains = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SecretDialogHint> _secretHints = new List<SecretDialogHint>();

    public int EventCount => _events.Count;
    public int SecretHintCount => _secretHints.Count;

    public void LoadFromFolder(string dataFolder)
    {
        _events.Clear();
        _secretEvents.Clear();
        _secretEventToRoute.Clear();
        _secretEventToWikiTitle.Clear();
        _secretChains.Clear();
        _secretHints.Clear();

        var eventsPath = IoUtil.Combine(dataFolder, "events_db.json");
        if (File.Exists(eventsPath))
            LoadEvents(File.ReadAllText(eventsPath));

        var secretPath = IoUtil.Combine(dataFolder, "secret_endings.json");
        if (File.Exists(secretPath))
            LoadSecrets(File.ReadAllText(secretPath));

        BuildSecretHintIndex();
    }

    public static string ResolveDataFolder(string pluginRoot)
    {
        var candidates = new[]
        {
            IoUtil.Combine(pluginRoot, "data"),
            IoUtil.Combine(IoUtil.Combine(pluginRoot, "MonsterPromHelper"), "data"),
        };
        foreach (var dir in candidates)
        {
            if (File.Exists(IoUtil.Combine(dir, "events_db.json")))
                return dir;
        }
        return candidates[0];
    }

    private void LoadEvents(string json)
    {
        foreach (Match block in Regex.Matches(json, "\"([^\"]+)\"\\s*:\\s*\\{"))
        {
            var key = block.Groups[1].Value;
            var start = block.Index;
            var slice = json.Substring(start, Math.Min(4000, json.Length - start));
            var rec = new EventRecord
            {
                Key = key,
                Name = MatchStr(slice, "name") ?? key,
                Route = MatchStr(slice, "route") ?? "",
                Type = MatchStr(slice, "type") ?? "",
            };

            foreach (Match opt in Regex.Matches(slice, "\\{\\s*\"option\"\\s*:\\s*(\\d+)"))
            {
                var optSlice = json.Substring(
                    start + opt.Index,
                    Math.Min(500, json.Length - start - opt.Index));
                rec.Options.Add(new OptionRecord
                {
                    Option = int.Parse(opt.Groups[1].Value),
                    Stat = MatchStr(optSlice, "stat") ?? "",
                    Hint = MatchStr(optSlice, "hint") ?? "",
                });
            }

            if (rec.Options.Count > 0)
            {
                _events[rec.Name] = rec;
                _events[rec.Key] = rec;
            }
        }
    }

    private void LoadSecrets(string json)
    {
        LoadSecretWikiChains(json);

        var allMatch = Regex.Match(json, "\"all\"\\s*:\\s*\\[([^\\]]+)\\]", RegexOptions.Singleline);
        if (allMatch.Success)
            AddSecretNamesFromBlock(allMatch.Groups[1].Value);

        var routeIdx = json.IndexOf("\"by_route\"");
        if (routeIdx < 0)
            return;

        var section = json.Substring(routeIdx, Math.Min(6000, json.Length - routeIdx));
        var allIdx = section.IndexOf("\"all\"");
        if (allIdx > 0)
            section = section.Substring(0, allIdx);

        foreach (Match rm in Regex.Matches(section, "\"([^\"]+)\"\\s*:\\s*\\[([^\\]]*)\\]", RegexOptions.Singleline))
        {
            var routeName = rm.Groups[1].Value;
            if (string.Equals(routeName, "by_route", StringComparison.OrdinalIgnoreCase))
                continue;

            var chain = new List<string>();
            foreach (Match em in Regex.Matches(rm.Groups[2].Value, "\"([^\"]+)\""))
            {
                var ev = em.Groups[1].Value;
                if (ev.Length == 0)
                    continue;
                chain.Add(ev);
                _secretEvents.Add(ev);
                _secretEventToRoute[ev] = routeName;
            }

            if (chain.Count > 0 && !_secretChains.ContainsKey(routeName))
                _secretChains[routeName] = chain;
        }
    }

    private void AddSecretNamesFromBlock(string block)
    {
        foreach (Match em in Regex.Matches(block, "\"([^\"]+)\""))
            _secretEvents.Add(em.Groups[1].Value);
    }

    private void LoadSecretWikiChains(string json)
    {
        var idx = json.IndexOf("\"chains\"");
        if (idx < 0)
            return;

        var section = json.Substring(idx, System.Math.Min(14000, json.Length - idx));
        foreach (Match block in Regex.Matches(section, "\\{[^{}]*\"wiki_title\"[^{}]*\\}", RegexOptions.Singleline))
        {
            var slice = block.Value;
            var wikiTitle = MatchStr(slice, "wiki_title");
            var character = MatchStr(slice, "character") ?? "";
            var eventsMatch = Regex.Match(slice, "\"events\"\\s*:\\s*\\[([^\\]]*)\\]", RegexOptions.Singleline);
            if (!MonoUtil.HasText(wikiTitle) || !eventsMatch.Success)
                continue;

            var chain = new List<string>();
            foreach (Match em in Regex.Matches(eventsMatch.Groups[1].Value, "\"([^\"]+)\""))
            {
                var ev = em.Groups[1].Value;
                if (ev.Length == 0)
                    continue;
                chain.Add(ev);
                _secretEvents.Add(ev);
                _secretEventToWikiTitle[ev] = wikiTitle!;
                if (MonoUtil.HasText(character))
                    _secretEventToRoute[ev] = character;
            }

            if (chain.Count > 0)
                _secretChains[wikiTitle!] = chain;
        }
    }

    public string? GetSecretWikiTitle(string? eventName)
    {
        if (!MonoUtil.HasText(eventName))
            return null;
        string title;
        if (_secretEventToWikiTitle.TryGetValue(eventName.Trim(), out title))
            return title;
        return null;
    }

    private static string? MatchStr(string slice, string key)
    {
        var m = Regex.Match(slice, $"\"{key}\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    public EventRecord? Get(string? name)
    {
        if (MonoUtil.IsNullOrWhiteSpace(name)) return null;
        return _events.TryGetValue(name.Trim(), out var ev) ? ev : null;
    }

    public bool IsSecretEnding(string? eventName) =>
        MonoUtil.HasText(eventName) && _secretEvents.Contains(eventName);

    public SecretAlertMatch? TryDetectSecret(LiveGameState live)
    {
        if (!live.EventActive || live.InCafeteriaPhase)
            return null;

        if (!DialogContext.IsSchoolEventDialog(live))
            return null;

        var o1 = (live.Option1Text ?? "").Trim();
        var o2 = (live.Option2Text ?? "").Trim();
        if (o1.Length < 10 || o2.Length < 10)
            return null;

        if (MonoUtil.HasText(live.EventName) && IsSecretEnding(live.EventName))
        {
            var evByName = Get(live.EventName);
            if (evByName != null)
            {
                var nameScore = ScoreOptionsAgainstEvent(evByName, o1, o2);
                if (nameScore >= 18)
                    return BuildSecretAlert(evByName, o1, o2, "ev:" + evByName.Name);
            }

            return null;
        }

        return MatchSecretByTwoOptions(o1, o2);
    }

    private SecretAlertMatch? MatchSecretByTwoOptions(string option1, string option2)
    {
        var scored = new List<KeyValuePair<int, EventRecord>>();
        foreach (var ev in SecretEventsOnly())
        {
            if (ev.Options.Count < 2)
                continue;

            var score = ScoreOptionsAgainstEvent(ev, option1, option2);
            if (score < 16)
                continue;

            scored.Add(new KeyValuePair<int, EventRecord>(score, ev));
        }

        if (scored.Count == 0)
            return null;

        scored.Sort((a, b) => b.Key.CompareTo(a.Key));
        var bestScore = scored[0].Key;
        var best = scored[0].Value;
        var secondScore = scored.Count > 1 ? scored[1].Key : 0;

        if (bestScore < 20)
            return null;
        if (scored.Count > 1 && bestScore < secondScore + 6)
            return null;

        return BuildSecretAlert(best, option1, option2, "dialog:" + best.Name);
    }

    private static int ScoreOptionsAgainstEvent(EventRecord ev, string option1, string option2)
    {
        if (ev.Options.Count < 2)
            return 0;

        var h1 = ev.Options[0].Hint ?? "";
        var h2 = ev.Options[1].Hint ?? "";
        if (h1.Length < 6 || h2.Length < 6)
            return 0;

        var ab = HintLineScore(h1, option1) + HintLineScore(h2, option2);
        var ba = HintLineScore(h1, option2) + HintLineScore(h2, option1);
        var score = ab > ba ? ab : ba;
        return score;
    }

    private SecretAlertMatch BuildSecretAlert(EventRecord ev, string option1, string option2, string dedupePrefix)
    {
        var wikiTitle = GetSecretWikiTitle(ev.Name);
        var route = GetSecretRouteName(ev.Name) ?? ev.Route;
        var chainKey = MonoUtil.HasText(wikiTitle) ? wikiTitle : route;
        var step = GetSecretStepIndex(ev.Name, chainKey);
        var total = MonoUtil.HasText(chainKey) && _secretChains.ContainsKey(chainKey)
            ? _secretChains[chainKey].Count
            : 0;
        var key = dedupePrefix + "|" + ev.Name + "|" + FingerprintDialog(option1, option2);
        var preview = TruncateOption(option1) + "  |  " + TruncateOption(option2);
        return BuildAlert(ev.Name, route, wikiTitle, step, total, 0, preview, key);
    }

    private static string TruncateOption(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        return text.Length <= 36 ? text : text.Substring(0, 33) + "...";
    }

    private static string FingerprintDialog(string option1, string option2)
    {
        var a = (option1 ?? "").Trim().ToLowerInvariant();
        var b = (option2 ?? "").Trim().ToLowerInvariant();
        if (a.Length > 48)
            a = a.Substring(0, 48);
        if (b.Length > 48)
            b = b.Substring(0, 48);
        return a + "||" + b;
    }

    private List<EventRecord> SecretEventsOnly()
    {
        var list = new List<EventRecord>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _events)
        {
            var ev = kv.Value;
            if (!IsSecretEventRecord(ev))
                continue;
            if (!seen.Add(ev.Name))
                continue;
            list.Add(ev);
        }
        return list;
    }

    private static SecretAlertMatch BuildAlert(
        string eventName,
        string route,
        string? wikiTitle,
        int step,
        int totalSteps,
        int option,
        string text,
        string dedupeKey)
    {
        return new SecretAlertMatch
        {
            EventName = eventName,
            Route = route,
            WikiTitle = wikiTitle ?? "",
            ChainStep = step,
            ChainTotal = totalSteps,
            MatchedOption = option,
            MatchedText = text ?? "",
            DedupeKey = dedupeKey,
        };
    }

    private void BuildSecretHintIndex()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _events)
        {
            var ev = kv.Value;
            if (!IsSecretEventRecord(ev))
                continue;

            var dedupeName = ev.Name;
            if (!seen.Add(dedupeName))
                continue;

            for (var i = 0; i < ev.Options.Count; i++)
            {
                var opt = ev.Options[i];
                if (!MonoUtil.HasText(opt.Hint))
                    continue;
                _secretHints.Add(new SecretDialogHint
                {
                    EventName = ev.Name,
                    Route = ev.Route,
                    Option = opt.Option,
                    Hint = opt.Hint,
                });
            }
        }
    }

    private bool IsSecretEventRecord(EventRecord ev)
    {
        if (IsSecretEnding(ev.Name))
            return true;
        return MonoUtil.HasText(ev.Type)
            && string.Equals(ev.Type.Trim(), "Secret Ending", StringComparison.OrdinalIgnoreCase);
    }

    public string? GetSecretRouteName(string? eventName)
    {
        if (!MonoUtil.HasText(eventName))
            return null;
        string route;
        return _secretEventToRoute.TryGetValue(eventName.Trim(), out route) ? route : null;
    }

    public string BuildSecretBanner(string? eventName, string? eventType)
    {
        var wiki = GetSecretWikiTitle(eventName);
        if (MonoUtil.HasText(wiki))
        {
            var step = GetSecretStepIndex(eventName, wiki);
            var total = _secretChains.ContainsKey(wiki) ? _secretChains[wiki].Count : 0;
            if (total > 0 && step > 0)
                return "SECRET: " + wiki + "  (" + step + "/" + total + ")";
            return "SECRET: " + wiki;
        }

        var route = GetSecretRouteName(eventName);
        if (route != null)
        {
            var step = GetSecretStepIndex(eventName, route);
            var total = _secretChains.ContainsKey(route) ? _secretChains[route].Count : 0;
            if (total > 0 && step > 0)
                return "SECRET-ROUTE: " + route + "  (" + step + "/" + total + ")";
            return "SECRET-ROUTE: " + route;
        }

        if (MonoUtil.HasText(eventName) && IsSecretEnding(eventName))
            return "SECRET-ENDING EVENT";

        if (MonoUtil.HasText(eventType)
            && eventType.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0)
            return "SECRET-ENDING (Event-Typ)";

        return "";
    }

    private int GetSecretStepIndex(string? eventName, string route)
    {
        if (!MonoUtil.HasText(eventName) || !_secretChains.ContainsKey(route))
            return 0;

        var chain = _secretChains[route];
        var name = eventName.Trim();
        for (var i = 0; i < chain.Count; i++)
        {
            if (string.Equals(chain[i], name, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return 0;
    }

    public Recommendation Recommend(EventRecord ev, IDictionary<string, int> stats)
    {
        var rec = new Recommendation();
        if (ev.Options.Count == 0) return rec;

        OptionRecord? best = null;
        var bestValue = int.MinValue;
        foreach (var opt in ev.Options)
        {
            var val = stats.TryGetValue(opt.Stat, out var v) ? v : 0;
            var maxRival = 0;
            foreach (var o in ev.Options)
            {
                if (o.Option == opt.Option) continue;
                if (stats.TryGetValue(o.Stat, out var rv) && rv > maxRival)
                    maxRival = rv;
            }
            string verdict;
            if (val > maxRival) verdict = "success";
            else if (val == maxRival) verdict = "tie";
            else verdict = "fail";

            rec.Lines.Add(new OptionLine
            {
                Option = opt.Option,
                Stat = opt.Stat,
                Value = val,
                Hint = opt.Hint,
                Verdict = verdict,
            });

            if (val > bestValue)
            {
                bestValue = val;
                best = opt;
            }
        }

        if (best != null)
        {
            rec.BestOption = best.Option;
            rec.BestStat = best.Stat;
            rec.BestValue = bestValue;
            rec.BestHint = best.Hint;
            foreach (var line in rec.Lines)
            {
                if (line.Option == best.Option)
                {
                    rec.BestVerdict = line.Verdict;
                    break;
                }
            }
        }

        return rec;
    }

    public List<EventRecord> SearchDialog(string query, int limit)
    {
        var results = new List<EventRecord>();
        var ranked = RankDialog(query, limit);
        foreach (var pair in ranked)
            results.Add(pair.Value);
        return results;
    }

    public HintMatchResult MatchByTwoOptions(
        string dialogText,
        string option1Line,
        string option2Line,
        bool relaxed)
    {
        var result = new HintMatchResult();
        var hay = (dialogText ?? "").ToLowerInvariant();
        var a = (option1Line ?? "").Trim();
        var b = (option2Line ?? "").Trim();
        var hasLines = a.Length >= 3 && b.Length >= 3;

        var scored = new List<KeyValuePair<int, EventRecord>>();
        foreach (var ev in UniqueEvents())
        {
            if (ev.Options.Count < 2)
                continue;

            var h1 = ev.Options[0].Hint ?? "";
            var h2 = ev.Options[1].Hint ?? "";
            if (h1.Length == 0 || h2.Length == 0)
                continue;

            var minLine = relaxed ? 8 : 10;
            var minFull = relaxed ? 4 : 5;
            int score;
            if (hasLines)
            {
                var ab = HintLineScore(h1, a) + HintLineScore(h2, b);
                var ba = HintLineScore(h1, b) + HintLineScore(h2, a);
                score = ab > ba ? ab : ba;
                if (score < minLine)
                    continue;
            }
            else
            {
                var s1 = HintLineScore(h1, hay);
                var s2 = HintLineScore(h2, hay);
                if (s1 < minFull || s2 < minFull)
                    continue;
                score = s1 + s2;
            }

            scored.Add(new KeyValuePair<int, EventRecord>(score, ev));
        }

        scored.Sort((x, y) =>
        {
            var c = y.Key.CompareTo(x.Key);
            return c != 0 ? c : string.Compare(x.Value.Name, y.Value.Name, StringComparison.OrdinalIgnoreCase);
        });

        for (var i = 0; i < scored.Count && i < 12; i++)
            result.Hits.Add(scored[i].Value);

        if (result.Hits.Count == 0)
        {
            result.Status = hasLines
                ? "No event matches the on-screen choices"
                : "No event — waiting for both choice buttons";
            return result;
        }

        var bestScore = scored[0].Key;
        var best = scored[0].Value;
        var secondScore = scored.Count > 1 ? scored[1].Key : 0;
        var pickScore = relaxed ? 11 : 14;
        var gap = relaxed ? 3 : 4;
        var soloScore = relaxed ? 14 : 18;

        if (bestScore >= pickScore && (scored.Count == 1 || bestScore >= secondScore + gap))
        {
            result.Best = best;
            result.Status = "ok";
            return result;
        }

        if (bestScore >= soloScore)
        {
            result.Best = best;
            result.Status = "ok";
            return result;
        }

        result.Status = "Multiple events possible — check the list";
        return result;
    }

    /// <summary>No yield — iterator state machines need CurrentManagedThreadId (.NET 4+).</summary>
    private List<EventRecord> UniqueEvents()
    {
        var list = new List<EventRecord>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _events)
        {
            if (!seen.Add(kv.Value.Name))
                continue;
            list.Add(kv.Value);
        }
        return list;
    }

    private List<KeyValuePair<int, EventRecord>> RankDialog(string query, int limit)
    {
        var results = new List<KeyValuePair<int, EventRecord>>();
        var q = (query ?? "").Trim().ToLowerInvariant();
        if (q.Length < 2)
            return results;

        var words = SplitWords(q);
        if (words.Count == 0)
            words.Add(q);

        foreach (var ev in UniqueEvents())
        {
            var name = ev.Name.ToLowerInvariant();
            var key = ev.Key.ToLowerInvariant();
            var route = (ev.Route ?? "").ToLowerInvariant();
            var etype = (ev.Type ?? "").ToLowerInvariant();
            var hints = new System.Text.StringBuilder();
            foreach (var opt in ev.Options)
                hints.Append((opt.Hint ?? "").ToLowerInvariant()).Append(' ');

            var haystack = name + " " + key + " " + route + " " + etype + " " + hints;

            var score = 0;
            if (q.Contains(name) || name.Contains(q) || q.Contains(key))
                score += 8;
            foreach (var word in words)
            {
                if (word.Length < 3)
                    continue;
                if (haystack.Contains(word))
                    score += 3;
                if (hints.ToString().Contains(word))
                    score += 4;
                if (route.Contains(word))
                    score += 2;
            }

            if (score > 0)
                results.Add(new KeyValuePair<int, EventRecord>(score, ev));
        }

        results.Sort((a, b) => b.Key.CompareTo(a.Key));
        if (results.Count > limit)
            results = results.GetRange(0, limit);
        return results;
    }

    private static List<string> SplitWords(string q)
    {
        var words = new List<string>();
        var part = new System.Text.StringBuilder();
        foreach (var ch in q)
        {
            if (char.IsLetterOrDigit(ch))
                part.Append(ch);
            else if (part.Length > 0)
            {
                if (part.Length >= 3)
                    words.Add(part.ToString());
                part.Length = 0;
            }
        }
        if (part.Length >= 3)
            words.Add(part.ToString());
        return words;
    }

    private static int HintLineScore(string hint, string line)
    {
        if (hint.Length == 0 || line.Length == 0)
            return 0;

        var h = hint.ToLowerInvariant().Trim();
        var ln = line.ToLowerInvariant().Trim();
        var score = 0;
        if (ln.Contains(h) || h.Contains(ln))
            score += 12;

        var hi = 0;
        var match = 0;
        var maxLen = h.Length < ln.Length ? h.Length : ln.Length;
        while (hi < h.Length && match < ln.Length && maxLen > 0)
        {
            if (h[hi] == ln[match])
            {
                score += 1;
                hi++;
            }
            match++;
        }

        foreach (var word in SplitWords(h))
        {
            if (ln.Contains(word))
                score += 3;
        }

        return score;
    }
}

public sealed class HintMatchResult
{
    public EventRecord? Best;
    public List<EventRecord> Hits = new List<EventRecord>();
    public string Status = "";
}

public sealed class Recommendation
{
    public List<OptionLine> Lines = new List<OptionLine>();
    public int BestOption;
    public string BestStat = "";
    public int BestValue;
    public string BestHint = "";
    public string BestVerdict = "";
}

public sealed class OptionLine
{
    public int Option;
    public string Stat = "";
    public int Value;
    public string Hint = "";
    public string Verdict = "";
}

public sealed class SecretDialogHint
{
    public string EventName = "";
    public string Route = "";
    public int Option;
    public string Hint = "";
}
