using System;
using DancingLine.Character;
using DancingLine.Level;
using DancingLine.UI;
using HarmonyLib;

namespace AutoPlay;

/// <summary>Harmony 补丁集合</summary>
internal static class AutoPlayPatches
{
    // ============================================
    // 关卡加载检测
    // ============================================

    /// <summary>
    /// LevelUI 收到关卡加载事件后调用，用于记录当前关卡并按当前开关状态启用自动游玩。
    /// </summary>
    [HarmonyPatch(typeof(LevelUI), "LevelBaseOnLevelLoadedEvent")]
    [HarmonyPostfix]
    private static void LevelUILoadedPostfix(LevelUI __instance, LevelBase level)
    {
        Plugin.Instance.Log.LogInfo((object)("[AutoPlay] 关卡加载: " + level.name));
        OnLevelLoaded(level);
    }

    /// <summary>
    /// LevelUI 收到关卡卸载事件后调用，用于清空当前关卡引用。
    /// </summary>
    [HarmonyPatch(typeof(LevelUI), "LevelBaseOnLevelUnloadedEvent")]
    [HarmonyPostfix]
    private static void LevelUIUnloadedPostfix()
    {
        Plugin.Instance.Log.LogInfo((object)"[AutoPlay] 关卡卸载");
        AutoPlayDriver.ClearLevel(Plugin.CurrentLevelBase);
        Plugin.CurrentLevelBase = null;
    }

    /// <summary>
    /// 额外钩子：LevelBase 事件注册通常表示关卡已激活，用于补充记录当前关卡。
    /// </summary>
    [HarmonyPatch(typeof(LevelBase), "add_LevelCompleteEvent")]
    [HarmonyPrefix]
    private static void LevelBaseAddCompletePrefix(LevelBase __instance)
    {
        if (Plugin.CurrentLevelBase == null)
        {
            Plugin.Instance.Log.LogInfo((object)("[AutoPlay] LevelBase 激活: " + __instance.name));
            OnLevelLoaded(__instance);
        }
    }

    /// <summary>
    /// 关卡开始前清理上一轮 AutoPlay 标记，避免影响下一次正常手动游玩。
    /// </summary>
    [HarmonyPatch(typeof(LevelBase), "StartLevel")]
    [HarmonyPrefix]
    private static void LevelBaseStartPrefix(LevelBase __instance)
    {
        AutoPlaySessionState.ResetBeforeNewRun(__instance);
    }

    /// <summary>
    /// 关卡正式开始时记录 LevelBase，作为 LevelUI 事件未触发时的可靠兜底入口。
    /// </summary>
    [HarmonyPatch(typeof(LevelBase), "StartLevel")]
    [HarmonyPostfix]
    private static void LevelBaseStartPostfix(LevelBase __instance)
    {
        OnLevelLoaded(__instance);
    }

    /// <summary>
    /// 关卡更新前驱动 AutoPlay，避免常驻组件 Update 顺序落在关卡推进之后造成一帧延迟。
    /// </summary>
    [HarmonyPatch(typeof(LevelBase), "OnUpdate")]
    [HarmonyPrefix]
    private static void LevelBaseUpdatePrefix(LevelBase __instance)
    {
        AutoPlayDriver.UpdateBeforeLevelFrame(__instance);
    }

    /// <summary>
    /// 关卡更新后补充记录正在游玩的 LevelBase，避免某些流程绕过 UI 加载事件。
    /// </summary>
    [HarmonyPatch(typeof(LevelBase), "OnUpdate")]
    [HarmonyPostfix]
    private static void LevelBaseUpdatePostfix(LevelBase __instance)
    {
        try
        {
            if (__instance != null && __instance.IsPlaying && Plugin.CurrentLevelBase != __instance)
            {
                OnLevelLoaded(__instance);
            }
        }
        catch (Exception) { }
    }

    // ============================================
    // 成就/分数阻止
    // ============================================

    /// <summary>
    /// AutoPlay 保护期内阻止字符串形式的成就上报，避免误触发成就。
    /// </summary>
    [HarmonyPatch(typeof(DancingLine.AchievementManager), "Report",
        new Type[] { typeof(string) })]
    [HarmonyPrefix]
    private static bool ReportStringPrefix(string id)
    {
        if (!AutoPlaySessionState.ShouldProtectGlobalProgress())
            return true;

        Plugin.Instance.Log.LogInfo((object)("[AutoPlay] 阻止游戏成就上报: " + SafeName(id)));
        return false;
    }

    /// <summary>
    /// AutoPlay 保护期内阻止带进度参数的成就上报，避免误触发成就。
    /// </summary>
    [HarmonyPatch(typeof(DancingLine.AchievementManager), "Report",
        new Type[] { typeof(string), typeof(int), typeof(int) })]
    [HarmonyPrefix]
    private static bool ReportFullPrefix(string id, int progress, int maxProgress)
    {
        if (!AutoPlaySessionState.ShouldProtectGlobalProgress())
            return true;

        Plugin.Instance.Log.LogInfo((object)("[AutoPlay] 阻止游戏成就进度: " + SafeName(id) + "=" + progress + "/" + maxProgress));
        return false;
    }

    /// <summary>
    /// 自动游玩开启时让游戏认为分数已经上报，降低结算分数写入风险。
    /// </summary>
    [HarmonyPatch(typeof(GameCharacter), "get_ReportedScore")]
    [HarmonyPostfix]
    private static void ReportedScoreGetterPostfix(ref bool __result)
    {
        if (AutoPlaySessionState.ShouldProtectGlobalProgress())
        {
            __result = true;
        }
    }

    // ============================================
    // 辅助
    // ============================================

    /// <summary>
    /// 记录当前关卡、刷新状态文本，并通知真实自动播放执行器绑定关卡。
    /// </summary>
    private static void OnLevelLoaded(LevelBase level)
    {
        Plugin.CurrentLevelBase = level;
        Plugin.Instance.Log.LogInfo((object)("[AutoPlay] 已记录: " + level.name));

        AutoPlayDriver.BindLevel(level);
        AutoPlayOverlay.EnsureCreated();
        Plugin.UpdateButtonText();
    }

    /// <summary>
    /// 获取成就名称用于日志，空名称用占位文本避免排查困难。
    /// </summary>
    private static string SafeName(string name)
    {
        return string.IsNullOrEmpty(name) ? "<empty>" : name;
    }
}
