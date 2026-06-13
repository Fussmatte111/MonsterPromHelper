using UnityEngine;

namespace MonsterCampHelper.Ingame;

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
        Plugin.Log.LogInfo("Overlay-Host fuer Text + GUI-Backdrop (OnGUI).");
    }

    private void Update()
    {
        if (MonoUtil.IsNull(Plugin.Instance))
            return;
        Plugin.Instance.TickFrame();
    }

    private void OnGUI()
    {
        if (MonoUtil.IsNull(Plugin.Instance))
            return;
        Plugin.Instance.DrawOverlayGui();
    }
}
