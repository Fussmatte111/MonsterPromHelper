using UnityEngine;

namespace MonsterPromHelper.Ingame;

[DefaultExecutionOrder(32000)]
public sealed class OverlayDrawHook : MonoBehaviour
{
    private void OnGUI()
    {
        if (MonoUtil.IsNull(Plugin.Instance))
            return;
        Plugin.Instance.DrawOverlayGui();
    }
}
