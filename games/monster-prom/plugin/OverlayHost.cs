using UnityEngine;

namespace MonsterPromHelper.Ingame;

/// <summary>Last OnGUI pass — draws full overlay (backdrop + text) above NGUI.</summary>
[DefaultExecutionOrder(32000)]
public sealed class OverlayHost : MonoBehaviour
{
    internal static OverlayHost? Instance { get; private set; }

    internal static void EnsureExists()
    {
        if (!MonoUtil.IsNull(Instance))
            return;

        var go = new GameObject("MPHelper_OverlayHost");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        Instance = go.AddComponent<OverlayHost>();
        Plugin.Log.LogInfo("Overlay host for text + GUI backdrop (OnGUI).");
    }

    private void OnGUI()
    {
        if (MonoUtil.IsNull(Plugin.Instance))
            return;
        Plugin.Instance.DrawOverlayGui();
    }
}
