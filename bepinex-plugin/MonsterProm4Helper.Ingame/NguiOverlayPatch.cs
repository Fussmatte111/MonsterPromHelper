namespace MonsterProm4Helper.Ingame;

public static class NguiOverlayPatch
{
    public static int HookCount { get; private set; }

    public static void TryApply()
    {
        Plugin.Log.LogInfo("Overlay: NGUI-Modus (MP4 — sichtbar ueber Spiel-UI).");
    }

    public static void AttachHooksToUiCameras(System.Type uiCamera)
    {
    }
}
