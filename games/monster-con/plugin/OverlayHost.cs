using UnityEngine;

namespace MonsterProm4Helper.Ingame;

/// <summary>DontDestroyOnLoad tick host — MP4 needs this when plugin Update does not run.</summary>
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
        Plugin.Log.LogInfo("OverlayHost (DontDestroyOnLoad) bereit.");
    }

    private void Update()
    {
        Plugin.Instance?.TickFrame();
    }
}
