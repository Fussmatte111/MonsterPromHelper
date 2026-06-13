namespace MonsterPromHelper.Ingame;

/// <summary>Locks game input via GeneralManager while the F8 overlay is open.</summary>
public static class GameInputLock
{
    private static bool _savedLocked;
    private static bool _hasSave;

    public static void Sync()
    {
        var gm = GeneralManager.Instance;
        if (gm == null)
            return;

        if (Plugin.OverlayOpen)
        {
            if (!_hasSave)
            {
                _savedLocked = gm.IsInputLocked;
                _hasSave = true;
            }

            gm.IsInputLocked = true;
            return;
        }

        if (_hasSave)
        {
            gm.IsInputLocked = _savedLocked;
            _hasSave = false;
        }
    }

    public static void Release()
    {
        if (!_hasSave)
            return;

        var gm = GeneralManager.Instance;
        if (gm != null)
            gm.IsInputLocked = _savedLocked;

        _hasSave = false;
    }
}
