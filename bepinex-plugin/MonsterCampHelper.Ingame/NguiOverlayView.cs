namespace MonsterCampHelper.Ingame;

/// <summary>Legacy NGUI layer — Camp helper uses IMGUI overlay only.</summary>
public static class NguiOverlayView
{
    public static bool IsActive => false;

    public static void ResetForNewScene()
    {
    }

    public static bool TryShow(string text) => false;

    public static void Hide()
    {
    }

    public static void Refresh(string text)
    {
    }
}
