using System.Collections.Generic;

namespace MonsterCampHelper.Ingame;

public sealed class OptionDisplayLine
{
    public int Option;
    public string Stat = "";
    public int Value;
    public string Hint = "";
    public string Verdict = "";
    public bool IsRecommended;
}

public sealed class OverlayViewModel
{
    public string Status = "";
    public string SceneName = "";
    public bool InSchool;
    public bool EventActive;
    public int DbEventCount;
    public string PlayerColor = "";
    public string DialogText = "";
    public string Option1Text = "";
    public string Option2Text = "";
    public string EventName = "";
    public string MatchSource = "";
    public string MatchNote = "";
    public string Route = "";
    public string EventType = "";
    public int EnginePick;
    public string EngineStat = "";
    public string StatsLine = "";
    public int RecommendedOption;
    public string RecommendedStat = "";
    public int RecommendedValue;
    public string RecommendedHint = "";
    public string RecommendedVerdict = "";
    public string SecretBanner = "";
    public bool OnSecretRoute;
    public List<OptionDisplayLine> DbOptions = new List<OptionDisplayLine>();
    public List<string> HitLines = new List<string>();
    public List<string> FooterLines = new List<string>();
}
