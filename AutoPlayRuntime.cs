using System;
using UnityEngine;

namespace AutoPlay;

internal sealed class AutoPlayRuntime : MonoBehaviour
{
    private int _lastToggleFrame = -1;

    /// <summary>
    /// Unity 每帧调用，负责保证状态文本存在、监听 F8 热键，并在关卡钩子失效时兜底驱动。
    /// </summary>
    private void Update()
    {
        AutoPlayOverlay.EnsureCreated();
        CheckToggleHotkey();
        AutoPlayDriver.Update();
    }

    /// <summary>
    /// 检查 F8 是否被按下，并用帧号防止同一帧内被多个入口重复切换。
    /// </summary>
    private void CheckToggleHotkey()
    {
        try
        {
            if (!Input.GetKeyDown(KeyCode.F8) || _lastToggleFrame == Time.frameCount)
                return;

            _lastToggleFrame = Time.frameCount;
            Plugin.Instance.Log.LogInfo((object)"[AutoPlay] F8 按下");
            Plugin.ToggleAutoPlay();
        }
        catch (Exception)
        {
            // 输入系统在极早期可能尚未初始化，等待下一帧继续尝试。
        }
    }
}
