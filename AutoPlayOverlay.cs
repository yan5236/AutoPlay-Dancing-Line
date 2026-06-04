using System;
using UnityEngine;
using UnityEngine.UI;

namespace AutoPlay;

internal static class AutoPlayOverlay
{
    private static bool _canvasCreated;

    /// <summary>
    /// 确保右上角状态文本已经创建；若 Canvas 曾因场景切换丢失，会重新创建。
    /// </summary>
    internal static void EnsureCreated()
    {
        if (_canvasCreated && Plugin.ButtonText != null)
            return;

        CreateStatusCanvas();
    }

    /// <summary>
    /// 创建常驻的屏幕覆盖 Canvas 和白色状态文本，用于确认插件是否已在游戏中生效。
    /// </summary>
    private static void CreateStatusCanvas()
    {
        try
        {
            Plugin.Instance.Log.LogInfo((object)"[AutoPlay] 创建状态 Canvas...");

            GameObject canvasObj = new GameObject("AutoPlayCanvas");

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObj.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(canvasObj);

            GameObject textObj = new GameObject("AutoPlayText");
            textObj.transform.SetParent(canvasObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(1f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(1f, 1f);
            textRect.anchoredPosition = new Vector2(-15f, -15f);
            textRect.sizeDelta = new Vector2(260f, 54f);

            Text txt = textObj.AddComponent<Text>();
            txt.text = Plugin.IsAutoPlayEnabled ? "AutoPlay: ON" : "AutoPlay: OFF";
            txt.fontSize = 22;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleRight;
            txt.raycastTarget = false;

            Font font = GetAnyFont();
            if (font != null)
            {
                txt.font = font;
                Plugin.Instance.Log.LogInfo((object)("[AutoPlay] 字体已设置: " + font.name));
            }
            else
            {
                Plugin.Instance.Log.LogWarning((object)"[AutoPlay] 未找到可用字体");
            }

            Plugin.ButtonText = txt;
            Plugin.UpdateButtonText();
            _canvasCreated = true;
            Plugin.Instance.Log.LogInfo((object)"[AutoPlay] 状态 Canvas 创建完成");
        }
        catch (Exception ex)
        {
            _canvasCreated = false;
            Plugin.Instance.Log.LogError((object)("[AutoPlay] Canvas 创建失败: " + ex));
        }
    }

    /// <summary>
    /// 获取 Unity 内置字体、系统 Arial 或现有 UI 字体，确保状态文本可以正常渲染。
    /// </summary>
    private static Font GetAnyFont()
    {
        try
        {
            Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin != null) return builtin;
        }
        catch (Exception) { }

        try
        {
            Font arial = Font.CreateDynamicFontFromOSFont("Arial", 14);
            if (arial != null) return arial;
        }
        catch (Exception) { }

        try
        {
            Text[] allTexts = UnityEngine.Object.FindObjectsOfType<Text>();
            foreach (Text t in allTexts)
            {
                if (t.font != null) return t.font;
            }
        }
        catch (Exception) { }

        return null;
    }
}
