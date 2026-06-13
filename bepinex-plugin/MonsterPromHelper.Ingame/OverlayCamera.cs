using UnityEngine;

namespace MonsterPromHelper.Ingame;

/// <summary>Disabled — backdrop is drawn in OverlayHost OnGUI only (whiteTexture caused full white screen).</summary>
public static class OverlayCamera
{
    public static void EnsureExists()
    {
        // Intentionally empty.
    }
}

public sealed class OverlayPostRender : MonoBehaviour
{
    private void OnPostRender()
    {
    }
}
