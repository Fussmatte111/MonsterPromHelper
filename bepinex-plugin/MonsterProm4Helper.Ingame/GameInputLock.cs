namespace MonsterProm4Helper.Ingame;

/// <summary>MP4 has no GeneralManager.IsInputLocked — input is gated via NGUI patches only.</summary>
public static class GameInputLock
{
    public static void Sync()
    {
    }

    public static void Release()
    {
    }
}
