using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using DancingLine.Level;
using HarmonyLib;

namespace AutoPlay;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "codex.dancingline.autoplay";
    public const string PluginName = "AutoPlay";
    public const string PluginVersion = "1.0.0";

    internal static bool IsAutoPlayEnabled;
    internal static LevelBase CurrentLevelBase;
    internal static UnityEngine.UI.Text ButtonText;

    private Harmony _harmony;

    internal static Plugin Instance { get; private set; }

    /// <summary>
    /// BepInEx 加载插件时调用，负责初始化补丁、常驻运行组件和状态文本。
    /// </summary>
    public override void Load()
    {
        Instance = this;
        Log.LogInfo((object)"[AutoPlay] Load() 开始");

        try
        {
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo((object)"[AutoPlay] Harmony 补丁已应用");
        }
        catch (Exception ex)
        {
            Log.LogError((object)("[AutoPlay] Harmony 初始化失败: " + ex));
            return;
        }

        try
        {
            AddComponent<AutoPlayRuntime>();
            Log.LogInfo((object)"[AutoPlay] 常驻运行组件已添加");
        }
        catch (Exception ex)
        {
            Log.LogError((object)("[AutoPlay] 常驻运行组件初始化失败: " + ex));
            return;
        }

        Log.LogInfo((object)"[AutoPlay] 插件加载完成");
    }

    /// <summary>
    /// 切换自动游玩开关，并同步刷新执行器与成绩保护状态。
    /// </summary>
    internal static void ToggleAutoPlay()
    {
        IsAutoPlayEnabled = !IsAutoPlayEnabled;
        AutoPlayDriver.ResetForToggle();
        Instance.Log.LogInfo((object)("[AutoPlay] 切换为: " + (IsAutoPlayEnabled ? "ON" : "OFF")));
        UpdateButtonText();

        if (IsAutoPlayEnabled)
        {
            AutoPlaySessionState.MarkAutoPlayUsed(CurrentLevelBase);

            if (CurrentLevelBase == null)
                Instance.Log.LogWarning((object)"[AutoPlay] 当前关卡暂未记录，将由自动播放执行器继续查找");
        }
        else
        {
            AutoPlaySessionState.ClearAutoPlayUse(CurrentLevelBase);
        }
    }

    /// <summary>
    /// 将当前自动游玩状态刷新到右上角状态文本。
    /// </summary>
    internal static void UpdateButtonText()
    {
        if (ButtonText != null)
        {
            ButtonText.text = IsAutoPlayEnabled ? "AutoPlay: ON" : "AutoPlay: OFF";
        }
    }

    /// <summary>
    /// 为指定关卡启用游戏自带的自动游玩控制器，缺失时会补加控制器组件。
    /// </summary>
    internal static void EnableAutoPlayController(LevelBase levelBase)
    {
        try
        {
            var controller = levelBase.autoPlayController;
            if (controller == null)
            {
                controller = levelBase.gameObject.AddComponent<DancingLine.GameDebug.Autoplay.AutoPlayController>();
                levelBase.autoPlayController = controller;
            }
            controller.enabled = true;
            levelBase.autoplayTime = 0f;
            Instance.Log.LogInfo((object)"[AutoPlay] AutoPlayController 已启用");
        }
        catch (Exception ex)
        {
            Instance.Log.LogError((object)("[AutoPlay] 启用失败: " + ex));
        }
    }

    /// <summary>
    /// 为指定关卡关闭游戏自带的自动游玩控制器，失败时只记录警告避免影响游戏流程。
    /// </summary>
    internal static void DisableAutoPlayController(LevelBase levelBase)
    {
        try
        {
            var controller = levelBase.autoPlayController;
            if (controller != null)
            {
                controller.enabled = false;
                Instance.Log.LogInfo((object)"[AutoPlay] AutoPlayController 已禁用");
            }
        }
        catch (Exception ex)
        {
            Instance.Log.LogWarning((object)("[AutoPlay] 禁用失败: " + ex.Message));
        }
    }
}
