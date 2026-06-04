using System;
using DancingLine.Character;
using DancingLine.Character.Customization;
using DancingLine.Level;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace AutoPlay;

internal static class AutoPlayDriver
{
    private const float PredictionPadding = 0.004f;
    private const float MaxPredictedStep = 0.12f;
    private const float StaleClickWindow = 0.18f;
    private const int MaxTriggersPerFrame = 12;

    private static LevelBase _boundLevel;
    private static Il2CppStructArray<float> _clickTimes;
    private static int _nextClickIndex;
    private static float _lastLevelTime = -1f;
    private static int _lastLevelUpdateFrame = -1;
    private static bool _loggedMissingLevel;
    private static bool _loggedMissingClickTimes;
    private static int _triggeredCount;

    /// <summary>
    /// 自动播放开关变化时调用，用于重置时间表缓存和一次性日志标记。
    /// </summary>
    internal static void ResetForToggle()
    {
        _clickTimes = null;
        _nextClickIndex = 0;
        _lastLevelTime = -1f;
        _lastLevelUpdateFrame = -1;
        _loggedMissingLevel = false;
        _loggedMissingClickTimes = false;
        _triggeredCount = 0;
    }

    /// <summary>
    /// 绑定当前正在运行的关卡，并在关卡变化时重置点击进度。
    /// </summary>
    internal static void BindLevel(LevelBase level)
    {
        if (level == null || level == _boundLevel)
            return;

        _boundLevel = level;
        Plugin.CurrentLevelBase = level;
        _clickTimes = null;
        _nextClickIndex = 0;
        _lastLevelTime = -1f;
        _lastLevelUpdateFrame = -1;
        _loggedMissingLevel = false;
        _loggedMissingClickTimes = false;
        _triggeredCount = 0;

        Plugin.Instance.Log.LogInfo((object)("[AutoPlay] 绑定关卡: " + level.name));

        if (Plugin.IsAutoPlayEnabled)
            AutoPlaySessionState.MarkAutoPlayUsed(level);
    }

    /// <summary>
    /// 关卡卸载或丢失时调用，用于清理当前关卡和点击时间表。
    /// </summary>
    internal static void ClearLevel(LevelBase level)
    {
        if (level != null && _boundLevel != level)
            return;

        _boundLevel = null;
        _clickTimes = null;
        _nextClickIndex = 0;
        _lastLevelTime = -1f;
        _lastLevelUpdateFrame = -1;
        _triggeredCount = 0;
    }

    /// <summary>
    /// 从常驻组件兜底驱动自动播放，只在关卡前置更新钩子没有运行时使用。
    /// </summary>
    internal static void Update()
    {
        if (_lastLevelUpdateFrame >= 0 && Time.frameCount - _lastLevelUpdateFrame <= 1)
            return;

        UpdateInternal(null);
    }

    /// <summary>
    /// 在关卡 OnUpdate 前驱动自动播放，让输入尽量发生在本帧关卡推进之前。
    /// </summary>
    internal static void UpdateBeforeLevelFrame(LevelBase level)
    {
        _lastLevelUpdateFrame = Time.frameCount;
        UpdateInternal(level);
    }

    /// <summary>
    /// 驱动自动播放：解析当前关卡、加载点击时间表，并预测本帧会越过的点击点。
    /// </summary>
    private static void UpdateInternal(LevelBase preferredLevel)
    {
        if (!Plugin.IsAutoPlayEnabled)
            return;

        LevelBase level = IsUsableLevel(preferredLevel) ? preferredLevel : ResolveCurrentLevel();
        if (level == null)
        {
            LogMissingLevelOnce();
            return;
        }

        BindLevel(level);

        if (!EnsureClickTimes(level))
            return;

        float levelTime = GetLevelTime(level);
        ResetIndexIfTimeRewound(levelTime);
        TriggerDueInputs(level, levelTime);
        _lastLevelTime = levelTime;
    }

    /// <summary>
    /// 尝试从补丁记录、当前角色和玩家容器中解析正在游玩的关卡。
    /// </summary>
    private static LevelBase ResolveCurrentLevel()
    {
        if (IsUsableLevel(Plugin.CurrentLevelBase))
            return Plugin.CurrentLevelBase;

        GameCharacter character = ResolveCurrentCharacter();
        if (character != null && IsUsableLevel(character.currentLevel))
            return character.currentLevel;

        return null;
    }

    /// <summary>
    /// 尝试获取当前玩家角色，优先使用游戏的 PlayerContainer，失败时回退到 GameCharacter 单例。
    /// </summary>
    private static GameCharacter ResolveCurrentCharacter()
    {
        try
        {
            GameCharacter current = PlayerContainer.Current;
            if (current != null)
                return current;
        }
        catch (Exception) { }

        try
        {
            GameCharacter current = GameCharacter.Instance;
            if (current != null)
                return current;
        }
        catch (Exception) { }

        return null;
    }

    /// <summary>
    /// 判断关卡是否适合自动播放，要求对象存在且处于实际游玩状态。
    /// </summary>
    private static bool IsUsableLevel(LevelBase level)
    {
        try
        {
            return level != null && level.IsPlaying;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 确保当前关卡的点击时间表已经加载，加载失败时只输出一次警告。
    /// </summary>
    private static bool EnsureClickTimes(LevelBase level)
    {
        if (_clickTimes != null && _clickTimes.Length > 0)
            return true;

        LineCustomizationController controller = FindLineCustomizationController();
        if (controller == null)
        {
            LogMissingClickTimesOnce("[AutoPlay] 未找到 LineCustomizationController，无法读取点击时间表");
            return false;
        }

        try
        {
            _clickTimes = controller.GetLevelClickTimes(level);
            if (_clickTimes == null || _clickTimes.Length == 0)
            {
                LogMissingClickTimesOnce("[AutoPlay] 当前关卡没有可用点击时间表");
                return false;
            }

            MoveIndexToNextPlayableClick(GetLevelTime(level));
            Plugin.Instance.Log.LogInfo((object)("[AutoPlay] 点击时间表已加载，数量: " + _clickTimes.Length));
            return true;
        }
        catch (Exception ex)
        {
            LogMissingClickTimesOnce("[AutoPlay] 读取点击时间表失败: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 查找线条自定义控制器，该控制器保存了每个关卡的自动点击时间表。
    /// </summary>
    private static LineCustomizationController FindLineCustomizationController()
    {
        try
        {
            return UnityEngine.Object.FindObjectOfType<LineCustomizationController>();
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogWarning((object)("[AutoPlay] 查找 LineCustomizationController 失败: " + ex.Message));
            return null;
        }
    }

    /// <summary>
    /// 获取当前关卡时间，优先使用 LevelBase.CurrentTime，失败时回退到角色 CurrentTime。
    /// </summary>
    private static float GetLevelTime(LevelBase level)
    {
        try
        {
            return level.CurrentTime;
        }
        catch (Exception) { }

        try
        {
            GameCharacter character = level.Haracter;
            if (character != null)
                return character.CurrentTime;
        }
        catch (Exception) { }

        return Time.time;
    }

    /// <summary>
    /// 当关卡时间倒退时重置点击下标，适配重开、复活和检查点跳转。
    /// </summary>
    private static void ResetIndexIfTimeRewound(float levelTime)
    {
        if (_lastLevelTime < 0f || levelTime + 0.15f >= _lastLevelTime)
            return;

        MoveIndexToNextPlayableClick(levelTime);
        Plugin.Instance.Log.LogInfo((object)("[AutoPlay] 检测到时间回退，点击下标重置为: " + _nextClickIndex));
    }

    /// <summary>
    /// 将点击下标移动到仍然可执行的时间点，避免加载稍晚时跳过贴近当前时间的转向。
    /// </summary>
    private static void MoveIndexToNextPlayableClick(float levelTime)
    {
        _nextClickIndex = 0;
        if (_clickTimes == null)
            return;

        SkipStaleClicks(levelTime);
    }

    /// <summary>
    /// 丢弃已经明显过期的点击点，避免检查点跳转或严重卡顿后一次性补出错误转向。
    /// </summary>
    private static void SkipStaleClicks(float levelTime)
    {
        if (_clickTimes == null)
            return;

        while (_nextClickIndex < _clickTimes.Length && _clickTimes[_nextClickIndex] < levelTime - StaleClickWindow)
        {
            _nextClickIndex++;
        }
    }

    /// <summary>
    /// 预测本帧关卡即将推进的时间长度，尽量在关卡时间越过点击点之前提前输入。
    /// </summary>
    private static float GetPredictedStep(float levelTime)
    {
        float fallbackStep = Mathf.Clamp(Time.deltaTime, 0f, MaxPredictedStep);
        if (_lastLevelTime < 0f)
            return fallbackStep;

        float observedStep = Mathf.Max(0f, levelTime - _lastLevelTime);
        if (observedStep <= 0f)
            return fallbackStep;

        return Mathf.Clamp(observedStep, 0f, MaxPredictedStep);
    }

    /// <summary>
    /// 触发本帧关卡推进前会越过的输入，每帧限制触发数量以避免卡顿后一次性连点过多。
    /// </summary>
    private static void TriggerDueInputs(LevelBase level, float levelTime)
    {
        SkipStaleClicks(levelTime);

        int triggersThisFrame = 0;
        float predictedTime = levelTime + GetPredictedStep(levelTime) + PredictionPadding;
        while (_clickTimes != null &&
               _nextClickIndex < _clickTimes.Length &&
               triggersThisFrame < MaxTriggersPerFrame)
        {
            float triggerTime = _clickTimes[_nextClickIndex];
            if (predictedTime < triggerTime)
                break;

            TriggerInput(level, triggerTime);
            _nextClickIndex++;
            triggersThisFrame++;
        }
    }

    /// <summary>
    /// 调用游戏自己的输入入口完成一次转向，失败时回退到角色点击方法。
    /// </summary>
    private static void TriggerInput(LevelBase level, float triggerTime)
    {
        try
        {
            level.InvokeInput(true);
            LogTriggeredInput(triggerTime);
            return;
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogWarning((object)("[AutoPlay] InvokeInput 失败，尝试角色点击: " + ex.Message));
        }

        try
        {
            GameCharacter character = level.Haracter ?? ResolveCurrentCharacter();
            if (character != null)
            {
                character.OnClicked();
                LogTriggeredInput(triggerTime);
            }
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogError((object)("[AutoPlay] 触发转向失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 输出自动转向日志，前几次详细记录，后续降低频率避免刷屏。
    /// </summary>
    private static void LogTriggeredInput(float triggerTime)
    {
        _triggeredCount++;
        if (_triggeredCount <= 5 || _triggeredCount % 25 == 0)
        {
            Plugin.Instance.Log.LogInfo((object)("[AutoPlay] 自动转向 #" + _triggeredCount + " @ " + triggerTime.ToString("0.000")));
        }
    }

    /// <summary>
    /// 当前关卡缺失时输出一次提示，避免每帧刷日志。
    /// </summary>
    private static void LogMissingLevelOnce()
    {
        if (_loggedMissingLevel)
            return;

        _loggedMissingLevel = true;
        Plugin.Instance.Log.LogWarning((object)"[AutoPlay] 尚未找到正在游玩的关卡，进入关卡后会继续尝试");
    }

    /// <summary>
    /// 点击时间表缺失时输出一次提示，避免每帧刷日志。
    /// </summary>
    private static void LogMissingClickTimesOnce(string message)
    {
        if (_loggedMissingClickTimes)
            return;

        _loggedMissingClickTimes = true;
        Plugin.Instance.Log.LogWarning((object)message);
    }
}
