using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MonsterCampHelper.Ingame;

/// <summary>Ctrl + left click on a drink shows its effect from drinks_db.json.</summary>
public static class DrinkInfoHelper
{
    private static readonly BindingFlags Inst =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static string _hoveredDrink = "";
    private static bool _visible;
    private static string _title = "";
    private static string _body = "";
    private static string _sub = "";
    private static float _hideAt;

    public static void ResetScene()
    {
        _hoveredDrink = "";
        _visible = false;
    }

    public static void SetHoveredDrink(string? drinkName)
    {
        _hoveredDrink = (drinkName ?? "").Trim();
    }

    public static void ShowForButton(object? button, DrinkDb db)
    {
        var name = ReadDrinkDisplayName(button);
        if (!MonoUtil.HasText(name))
            return;
        SetHoveredDrink(name);
        ShowLookup(db, name);
    }

    public static void Tick(DrinkDb db, bool enabled)
    {
        if (!enabled || db == null)
        {
            _visible = false;
            return;
        }

        if (Plugin.OverlayOpen)
        {
            _visible = false;
            return;
        }

        if (_visible && Time.unscaledTime >= _hideAt)
            _visible = false;

        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (!MonoUtil.HasText(_hoveredDrink))
            return;

        ShowLookup(db, _hoveredDrink);
    }

    private static void ShowLookup(DrinkDb db, string drinkName)
    {
        var rec = db.Get(drinkName);
        if (rec == null)
        {
            _visible = true;
            _title = drinkName;
            _body = "No entry in drinks_db.json";
            _sub = "Ctrl+click";
            _hideAt = Time.unscaledTime + 8f;
            return;
        }

        _visible = true;
        _title = rec.Name;
        _body = MonoUtil.HasText(rec.Effect) ? rec.Effect : "No effect documented";
        _sub = MonoUtil.HasText(rec.Misc) ? rec.Misc : "Ctrl+click = drink info";
        _hideAt = Time.unscaledTime + 12f;
    }

    public static void Draw()
    {
        if (!_visible)
            return;
        OverlayGui.DrawPickHint(_title, _body, _sub);
    }

    internal static string? ReadDrinkDisplayName(object? button)
    {
        if (button == null)
            return null;

        var profile = GameBridge.ReadMemberPublic(button, "drinkProfile");
        if (profile == null)
            return null;

        try
        {
            var method = profile.GetType().GetMethod("GetDisplayName", Inst);
            if (method != null)
            {
                var display = method.Invoke(profile, null) as string;
                if (MonoUtil.HasText(display))
                    return display;
            }

            var id = GameBridge.ReadMemberPublic(profile, "id") as string;
            if (MonoUtil.HasText(id))
                return id;
        }
        catch
        {
        }

        return null;
    }
}

internal static class DrinkHoverPatch
{
    private static bool _patched;

    public static void TryApply()
    {
        if (_patched)
            return;

        try
        {
            var handlerType = AccessTools.TypeByName("BeautifulGlitch.ButtonHandler");
            var buttonType = AccessTools.TypeByName("MonsterCamp.DrinksChoiceButton");
            if (handlerType == null || buttonType == null)
            {
                Plugin.Log.LogWarning("Drink UI types not found.");
                return;
            }

            var hover = AccessTools.Method(handlerType, "PlayHoverOver");
            var confirm = AccessTools.Method(buttonType, "UiConfirmItem");
            var count = 0;

            if (hover != null)
            {
                Plugin.Harmony.Patch(
                    hover,
                    postfix: new HarmonyMethod(typeof(DrinkHoverPatch), nameof(HoverPostfix)));
                count++;
            }

            if (confirm != null)
            {
                Plugin.Harmony.Patch(
                    confirm,
                    prefix: new HarmonyMethod(typeof(DrinkHoverPatch), nameof(ConfirmPrefix)));
                count++;
            }

            if (count == 0)
            {
                Plugin.Log.LogWarning("Drink hooks not found.");
                return;
            }

            _patched = true;
            Plugin.Log.LogInfo("Drink hooks active (" + count + ").");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning("Drink hooks: " + ex.Message);
        }
    }

    private static void HoverPostfix(object __instance)
    {
        if (__instance == null || __instance.GetType().Name != "DrinksChoiceButton")
            return;

        var name = DrinkInfoHelper.ReadDrinkDisplayName(__instance);
        if (MonoUtil.HasText(name))
            DrinkInfoHelper.SetHoveredDrink(name);
    }

    private static bool ConfirmPrefix(object __instance)
    {
        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
            return true;

        var db = Plugin.Instance?.Drinks;
        if (db == null)
            return true;

        DrinkInfoHelper.ShowForButton(__instance, db);
        return false;
    }
}
