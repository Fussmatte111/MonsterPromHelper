using Game;
using HarmonyLib;

namespace MonsterProm4Helper.Ingame;

/// <summary>MP4: BaseUnityPlugin.Update is unreliable — tick via GeneralManager.Update.</summary>
internal static class PluginTickPatch
{
    private static bool _patched;

    public static void TryApply()
    {
        if (_patched)
            return;

        try
        {
            var update = AccessTools.Method(typeof(GeneralManager), "Update");
            if (update == null)
            {
                Plugin.Log.LogWarning("GeneralManager.Update not found — OverlayHost tick only.");
                return;
            }

            Plugin.Harmony.Patch(
                update,
                postfix: new HarmonyMethod(typeof(PluginTickPatch), nameof(Postfix)));
            _patched = true;
            Plugin.Log.LogInfo("Tick hook on GeneralManager.Update.");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning("Tick-Hook: " + ex.Message);
        }
    }

    private static void Postfix()
    {
        var plugin = Plugin.Instance;
        if (plugin != null)
            plugin.TickFrame();
    }
}
