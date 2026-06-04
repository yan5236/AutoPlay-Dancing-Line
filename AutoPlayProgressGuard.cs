using System;
using System.Collections.Generic;
using DancingLine.Character;
using DancingLine.Level;
using HarmonyLib;

namespace AutoPlay;

/// <summary>AutoPlay 开启时的成绩与成就保护补丁。</summary>
internal static class AutoPlayProgressGuard
{
    private static readonly HashSet<string> BlockedSaveKeys = new HashSet<string>();

    /// <summary>
    /// 自动游玩开启时跳过关卡成就处理，避免通关、完美和收集类成就被原流程上报。
    /// </summary>
    [HarmonyPatch(typeof(LevelBase), "HandleAchievements")]
    [HarmonyPrefix]
    private static bool LevelBaseHandleAchievementsPrefix(LevelBase __instance)
    {
        return !IsAutoPlayActiveForLevel(__instance);
    }

    /// <summary>
    /// 自动游玩开启时阻止写入关卡已完成标记，避免生成通关记录。
    /// </summary>
    [HarmonyPatch(typeof(LevelBase), "set_IsCompleted")]
    [HarmonyPrefix]
    private static bool LevelBaseSetIsCompletedPrefix(LevelBase __instance, bool value)
    {
        if (!value || !IsAutoPlayActiveForLevel(__instance))
            return true;

        LogBlockedProgress("阻止写入通关记录: " + SafeLevelName(__instance));
        return false;
    }

    /// <summary>
    /// 自动游玩开启时阻止写入历史完美标记，避免生成完美通关记录。
    /// </summary>
    [HarmonyPatch(typeof(LevelBase), "set_IsPerfectReachedBefore")]
    [HarmonyPrefix]
    private static bool LevelBaseSetIsPerfectReachedBeforePrefix(LevelBase __instance, bool value)
    {
        if (!value || !IsAutoPlayActiveForLevel(__instance))
            return true;

        LogBlockedProgress("阻止写入完美记录: " + SafeLevelName(__instance));
        return false;
    }

    /// <summary>
    /// 自动游玩开启时让本轮完美状态始终为否，避免结算和成就读取到完美通关。
    /// </summary>
    [HarmonyPatch(typeof(LevelBase), "get_IsPerfectOnCurrentRun")]
    [HarmonyPostfix]
    private static void LevelBaseGetIsPerfectOnCurrentRunPostfix(LevelBase __instance, ref bool __result)
    {
        if (IsAutoPlayActiveForLevel(__instance))
            __result = false;
    }

    /// <summary>
    /// 自动游玩开启时阻止角色分数被标记为已上报，避免绕过现有读取保护直接写分数状态。
    /// </summary>
    [HarmonyPatch(typeof(GameCharacter), "set_ReportedScore")]
    [HarmonyPrefix]
    private static bool GameCharacterSetReportedScorePrefix(bool value)
    {
        if (!value || !Plugin.IsAutoPlayEnabled)
            return true;

        LogBlockedProgress("阻止写入分数上报状态");
        return false;
    }

    /// <summary>
    /// 自动游玩开启时拦截枚举形式的进度存档，常见于关卡、成就或奖励状态。
    /// </summary>
    [HarmonyPatch(typeof(UserDataManager), "Save",
        new Type[] { typeof(string), typeof(Type), typeof(Enum), typeof(bool) })]
    [HarmonyPrefix]
    private static bool UserDataSaveEnumPrefix(string name)
    {
        return !ShouldBlockProgressSave(name);
    }

    /// <summary>
    /// 自动游玩开启时拦截时间形式的进度存档，避免记录完成时间或刷新时间类成就。
    /// </summary>
    [HarmonyPatch(typeof(UserDataManager), "Save",
        new Type[] { typeof(string), typeof(DateTime), typeof(bool) })]
    [HarmonyPrefix]
    private static bool UserDataSaveDateTimePrefix(string name)
    {
        return !ShouldBlockProgressSave(name);
    }

    /// <summary>
    /// 自动游玩开启时拦截浮点形式的进度存档，避免写入百分比或分数。
    /// </summary>
    [HarmonyPatch(typeof(UserDataManager), "Save",
        new Type[] { typeof(string), typeof(float), typeof(bool) })]
    [HarmonyPrefix]
    private static bool UserDataSaveFloatPrefix(string name)
    {
        return !ShouldBlockProgressSave(name);
    }

    /// <summary>
    /// 自动游玩开启时拦截整数形式的进度存档，避免写入收集、分数或统计计数。
    /// </summary>
    [HarmonyPatch(typeof(UserDataManager), "Save",
        new Type[] { typeof(string), typeof(int), typeof(bool) })]
    [HarmonyPrefix]
    private static bool UserDataSaveIntPrefix(string name)
    {
        return !ShouldBlockProgressSave(name);
    }

    /// <summary>
    /// 自动游玩开启时拦截字符串形式的进度存档，避免写入关卡进度快照。
    /// </summary>
    [HarmonyPatch(typeof(UserDataManager), "Save",
        new Type[] { typeof(string), typeof(string), typeof(bool) })]
    [HarmonyPrefix]
    private static bool UserDataSaveStringPrefix(string name)
    {
        return !ShouldBlockProgressSave(name);
    }

    /// <summary>
    /// 自动游玩开启时拦截布尔形式的进度存档，避免写入通关、完美或成就标记。
    /// </summary>
    [HarmonyPatch(typeof(UserDataManager), "Save",
        new Type[] { typeof(string), typeof(bool), typeof(bool) })]
    [HarmonyPrefix]
    private static bool UserDataSaveBoolPrefix(string name)
    {
        return !ShouldBlockProgressSave(name);
    }

    /// <summary>
    /// 判断 AutoPlay 是否正作用于指定关卡，关闭开关后立即允许手动成绩写入。
    /// </summary>
    private static bool IsAutoPlayActiveForLevel(LevelBase level)
    {
        return AutoPlaySessionState.ShouldProtectProgress(level);
    }

    /// <summary>
    /// AutoPlay 保护期内阻止所有用户数据写入，并记录具体 key 方便定位漏网进度。
    /// </summary>
    private static bool ShouldBlockProgressSave(string name)
    {
        if (!AutoPlaySessionState.ShouldProtectGlobalProgress())
            return false;

        LogBlockedProgress("阻止用户数据存档: " + SafeSaveName(name));
        return true;
    }

    /// <summary>
    /// 获取存档 key 用于日志，空 key 用占位文本避免日志不完整。
    /// </summary>
    private static string SafeSaveName(string name)
    {
        return string.IsNullOrEmpty(name) ? "<empty>" : name;
    }

    /// <summary>
    /// 获取关卡名称用于日志，读取失败时返回通用占位文本。
    /// </summary>
    private static string SafeLevelName(LevelBase level)
    {
        try
        {
            return level != null ? level.name : "<unknown>";
        }
        catch (Exception)
        {
            return "<unknown>";
        }
    }

    /// <summary>
    /// 输出被拦截的进度写入日志，同一个保存 key 只记录一次避免刷屏。
    /// </summary>
    private static void LogBlockedProgress(string message)
    {
        if (!BlockedSaveKeys.Add(message))
            return;

        Plugin.Instance.Log.LogInfo((object)("[AutoPlay] " + message));
    }
}
