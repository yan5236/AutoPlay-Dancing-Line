using System.Collections.Generic;
using DancingLine;
using HarmonyLib;

namespace AutoPlay;

/// <summary>AutoPlay 保护期内的关卡记录数据拦截补丁。</summary>
internal static class AutoPlayLevelDataGuard
{
    private static readonly HashSet<string> BlockedLevelDataWrites = new HashSet<string>();

    /// <summary>
    /// AutoPlay 保护期内阻止关卡数据保存，避免主界面进度被持久化。
    /// </summary>
    [HarmonyPatch(typeof(LevelData), "Save")]
    [HarmonyPrefix]
    private static bool LevelDataSavePrefix(LevelData __instance)
    {
        if (!ShouldProtectLevelData(__instance))
            return true;

        LogBlockedLevelData("阻止 LevelData.Save: " + SafeLevelName(__instance));
        return false;
    }

    /// <summary>
    /// AutoPlay 保护期内阻止写入已通关状态。
    /// </summary>
    [HarmonyPatch(typeof(LevelData), "set_IsCompleted")]
    [HarmonyPrefix]
    private static bool LevelDataSetIsCompletedPrefix(LevelData __instance, bool value)
    {
        if (!value || !ShouldProtectLevelData(__instance))
            return true;

        LogBlockedLevelData("阻止 LevelData 通关: " + SafeLevelName(__instance));
        return false;
    }

    /// <summary>
    /// AutoPlay 保护期内阻止通过原始字段属性写入已通关状态。
    /// </summary>
    [HarmonyPatch(typeof(LevelData), "set_isCompleted")]
    [HarmonyPrefix]
    private static bool LevelDataSetRawIsCompletedPrefix(LevelData __instance, bool value)
    {
        return LevelDataSetIsCompletedPrefix(__instance, value);
    }

    /// <summary>
    /// AutoPlay 保护期内阻止写入完美状态。
    /// </summary>
    [HarmonyPatch(typeof(LevelData), "set_Perfect")]
    [HarmonyPrefix]
    private static bool LevelDataSetPerfectPrefix(LevelData __instance, bool value)
    {
        if (!value || !ShouldProtectLevelData(__instance))
            return true;

        LogBlockedLevelData("阻止 LevelData 完美: " + SafeLevelName(__instance));
        return false;
    }

    /// <summary>
    /// AutoPlay 保护期内阻止通过原始字段属性写入完美状态。
    /// </summary>
    [HarmonyPatch(typeof(LevelData), "set_perfect")]
    [HarmonyPrefix]
    private static bool LevelDataSetRawPerfectPrefix(LevelData __instance, bool value)
    {
        return LevelDataSetPerfectPrefix(__instance, value);
    }

    /// <summary>
    /// AutoPlay 保护期内阻止提升最高百分比，避免主界面显示自动游玩 100%。
    /// </summary>
    [HarmonyPatch(typeof(LevelData), "set_Percent")]
    [HarmonyPrefix]
    private static bool LevelDataSetPercentPrefix(LevelData __instance, int value)
    {
        if (value <= SafePercent(__instance) || !ShouldProtectLevelData(__instance))
            return true;

        LogBlockedLevelData("阻止 LevelData 百分比: " + SafeLevelName(__instance) + "=" + value);
        return false;
    }

    /// <summary>
    /// AutoPlay 保护期内阻止通过原始字段属性提升最高百分比。
    /// </summary>
    [HarmonyPatch(typeof(LevelData), "set_percent")]
    [HarmonyPrefix]
    private static bool LevelDataSetRawPercentPrefix(LevelData __instance, int value)
    {
        return LevelDataSetPercentPrefix(__instance, value);
    }

    /// <summary>
    /// AutoPlay 保护期内阻止提升星星或皇冠收集记录。
    /// </summary>
    [HarmonyPatch(typeof(LevelData), "set_StarsCollected")]
    [HarmonyPrefix]
    private static bool LevelDataSetStarsCollectedPrefix(LevelData __instance, int value)
    {
        if (value <= SafeStars(__instance) || !ShouldProtectLevelData(__instance))
            return true;

        LogBlockedLevelData("阻止 LevelData 星星: " + SafeLevelName(__instance) + "=" + value);
        return false;
    }

    /// <summary>
    /// AutoPlay 保护期内阻止通过原始字段属性提升星星或皇冠收集记录。
    /// </summary>
    [HarmonyPatch(typeof(LevelData), "set_stars")]
    [HarmonyPrefix]
    private static bool LevelDataSetRawStarsPrefix(LevelData __instance, int value)
    {
        return LevelDataSetStarsCollectedPrefix(__instance, value);
    }

    /// <summary>
    /// AutoPlay 保护期内阻止提升钻石收集记录。
    /// </summary>
    [HarmonyPatch(typeof(LevelData), "set_DiamontsCollected")]
    [HarmonyPrefix]
    private static bool LevelDataSetDiamontsCollectedPrefix(LevelData __instance, int value)
    {
        if (value <= SafeDiamonts(__instance) || !ShouldProtectLevelData(__instance))
            return true;

        LogBlockedLevelData("阻止 LevelData 钻石: " + SafeLevelName(__instance) + "=" + value);
        return false;
    }

    /// <summary>
    /// AutoPlay 保护期内阻止通过原始字段属性提升钻石收集记录。
    /// </summary>
    [HarmonyPatch(typeof(LevelData), "set_diamonds")]
    [HarmonyPrefix]
    private static bool LevelDataSetRawDiamondsPrefix(LevelData __instance, int value)
    {
        return LevelDataSetDiamontsCollectedPrefix(__instance, value);
    }

    /// <summary>
    /// 判断指定关卡数据是否处于 AutoPlay 保护期。
    /// </summary>
    private static bool ShouldProtectLevelData(LevelData data)
    {
        if (!AutoPlaySessionState.ShouldProtectGlobalProgress())
            return false;

        try
        {
            return AutoPlaySessionState.ShouldProtectProgress(data != null ? data.m_Level : null);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// 安全读取当前百分比，读取失败时返回最小值以优先保护记录。
    /// </summary>
    private static int SafePercent(LevelData data)
    {
        try
        {
            return data != null ? data.Percent : int.MinValue;
        }
        catch
        {
            return int.MinValue;
        }
    }

    /// <summary>
    /// 安全读取当前星星或皇冠数量，读取失败时返回最小值以优先保护记录。
    /// </summary>
    private static int SafeStars(LevelData data)
    {
        try
        {
            return data != null ? data.StarsCollected : int.MinValue;
        }
        catch
        {
            return int.MinValue;
        }
    }

    /// <summary>
    /// 安全读取当前钻石数量，读取失败时返回最小值以优先保护记录。
    /// </summary>
    private static int SafeDiamonts(LevelData data)
    {
        try
        {
            return data != null ? data.DiamontsCollected : int.MinValue;
        }
        catch
        {
            return int.MinValue;
        }
    }

    /// <summary>
    /// 获取关卡名称用于日志，读取失败时返回通用占位文本。
    /// </summary>
    private static string SafeLevelName(LevelData data)
    {
        try
        {
            return data != null && data.m_Level != null ? data.m_Level.name : "<unknown>";
        }
        catch
        {
            return "<unknown>";
        }
    }

    /// <summary>
    /// 输出 LevelData 拦截日志，同一条记录只输出一次避免刷屏。
    /// </summary>
    private static void LogBlockedLevelData(string message)
    {
        if (!BlockedLevelDataWrites.Add(message))
            return;

        Plugin.Instance.Log.LogInfo((object)("[AutoPlay] " + message));
    }
}
