using System;
using DancingLine.Level;

namespace AutoPlay;

/// <summary>记录当前 AutoPlay 正在保护的关卡。</summary>
internal static class AutoPlaySessionState
{
    private static LevelBase _protectedLevel;

    /// <summary>
    /// 记录当前开关开启时作用的关卡，用于限制成绩保护范围。
    /// </summary>
    internal static void MarkAutoPlayUsed(LevelBase level)
    {
        if (level == null)
            return;

        if (_protectedLevel == level)
            return;

        _protectedLevel = level;
        Plugin.Instance.Log.LogInfo((object)("[AutoPlay] 本轮已标记为 AutoPlay: " + SafeLevelName(level)));
    }

    /// <summary>
    /// 清理当前关卡保护状态，避免关闭 AutoPlay 后继续阻止手动成绩写入。
    /// </summary>
    internal static void ClearAutoPlayUse(LevelBase level)
    {
        if (_protectedLevel == null)
            return;

        Plugin.Instance.Log.LogInfo((object)("[AutoPlay] 清理 AutoPlay 标记: " + SafeLevelName(level)));
        _protectedLevel = null;
    }

    /// <summary>
    /// 新一轮关卡开始前清理旧保护状态，避免影响后续正常手动游玩。
    /// </summary>
    internal static void ResetBeforeNewRun(LevelBase level)
    {
        ClearAutoPlayUse(level);
    }

    /// <summary>
    /// 判断当前是否应该保护指定关卡的成绩写入，只在 AutoPlay 开关开启时生效。
    /// </summary>
    internal static bool ShouldProtectProgress(LevelBase level)
    {
        if (!Plugin.IsAutoPlayEnabled)
            return false;

        if (_protectedLevel == null)
            return true;

        return level == null || level == _protectedLevel;
    }

    /// <summary>
    /// 判断任意全局进度写入是否应被保护，用于没有关卡参数的存档和 Steam API。
    /// </summary>
    internal static bool ShouldProtectGlobalProgress()
    {
        return Plugin.IsAutoPlayEnabled;
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
}
