using System.Collections.Generic;
using HarmonyLib;
using Steamworks;

namespace AutoPlay;

/// <summary>AutoPlay 保护期内的 Steam 成就与统计拦截补丁。</summary>
internal static class AutoPlaySteamGuard
{
    private static readonly HashSet<string> BlockedSteamCalls = new HashSet<string>();

    /// <summary>
    /// AutoPlay 保护期内阻止 Steam 成就解锁，并记录被拦截的成就名称。
    /// </summary>
    [HarmonyPatch(typeof(SteamUserStats), "SetAchievement")]
    [HarmonyPrefix]
    private static bool SteamSetAchievementPrefix(string pchName, ref bool __result)
    {
        if (!AutoPlaySessionState.ShouldProtectGlobalProgress())
            return true;

        __result = false;
        LogBlockedSteamCall("阻止 Steam 成就: " + SafeName(pchName));
        return false;
    }

    /// <summary>
    /// AutoPlay 保护期内阻止 Steam 整数统计写入，避免统计型成就继续推进。
    /// </summary>
    [HarmonyPatch(typeof(SteamUserStats), "SetStat")]
    [HarmonyPrefix]
    private static bool SteamSetStatPrefix(string pchName, int nData, ref bool __result)
    {
        if (!AutoPlaySessionState.ShouldProtectGlobalProgress())
            return true;

        __result = false;
        LogBlockedSteamCall("阻止 Steam 统计: " + SafeName(pchName) + "=" + nData);
        return false;
    }

    /// <summary>
    /// AutoPlay 保护期内阻止 Steam 统计提交，避免之前排队的成就或统计被上传。
    /// </summary>
    [HarmonyPatch(typeof(SteamUserStats), "StoreStats")]
    [HarmonyPrefix]
    private static bool SteamStoreStatsPrefix(ref bool __result)
    {
        if (!AutoPlaySessionState.ShouldProtectGlobalProgress())
            return true;

        __result = false;
        LogBlockedSteamCall("阻止 Steam StoreStats");
        return false;
    }

    /// <summary>
    /// 获取 Steam 成就或统计名称用于日志，空名称用占位文本避免排查困难。
    /// </summary>
    private static string SafeName(string name)
    {
        return string.IsNullOrEmpty(name) ? "<empty>" : name;
    }

    /// <summary>
    /// 输出 Steam 拦截日志，同一个调用只记录一次避免结算时刷屏。
    /// </summary>
    private static void LogBlockedSteamCall(string message)
    {
        if (!BlockedSteamCalls.Add(message))
            return;

        Plugin.Instance.Log.LogInfo((object)("[AutoPlay] " + message));
    }
}
