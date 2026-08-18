using System.Runtime.InteropServices;
using UnityEngine;

namespace MonsterProm4Helper.Ingame;

/// <summary>F8/F9 via Win32 — works when Unity Input does not receive keys (MP4 / Steam overlay).</summary>
internal static class NativeKeyPoll
{
    private const int VkF8 = 0x77;
    private const int VkF9 = 0x78;

    private static bool _f8WasDown;
    private static bool _f9WasDown;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public static void Tick(Plugin plugin)
    {
        if (plugin == null)
            return;

        var f8 = IsDown(VkF8);
        var f9 = IsDown(VkF9);

        if (f8 && !_f8WasDown)
            plugin.TryHandleToggleKey(KeyCode.F8, "Win32");
        else if (f9 && !_f9WasDown)
            plugin.TryHandleToggleKey(KeyCode.F9, "Win32");

        _f8WasDown = f8;
        _f9WasDown = f9;
    }

    private static bool IsDown(int vk)
    {
        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }
}
