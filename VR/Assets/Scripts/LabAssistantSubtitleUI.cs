using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Makes the Lab Assistant dialogue readable on desktop by adjusting the
/// world-space bubble position, size, and font.
/// </summary>
public class LabAssistantSubtitleUI : MonoBehaviour
{
    private const string LabAssistantSceneName = "LabAssistantScene";
    private const string BubbleTextObjectName = "TxtData";
    private const string SpeakerTextObjectName = "TxtName";
    private const float TargetBubbleCanvasScale = 0.00145f;
    private const float TargetChatScreenScale = 0.58f;
    private const float MinimumBubbleHeight = 2.8f;
    private const float RescanIntervalSeconds = 0.75f;

    private Transform bubbleCanvasTransform;
    private RectTransform chatScreenTransform;
    private TMP_Text[] cachedBubbleTexts = Array.Empty<TMP_Text>();
    private TMP_Text[] cachedWorldBubbleUiTexts = Array.Empty<TMP_Text>();
    private float nextRescanTime;

    void Start()
    {
        RemoveLegacySubtitleOverlay();
        RefreshSceneReferences(forceRefreshFont: true);
    }

    void RemoveLegacySubtitleOverlay()
    {
        Transform subtitlePanel = transform.Find("SubtitlePanel");
        if (subtitlePanel != null)
        {
            Destroy(subtitlePanel.gameObject);
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            Destroy(canvas);
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            Destroy(scaler);
        }

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            Destroy(raycaster);
        }
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name != LabAssistantSceneName)
        {
            return;
        }

        if (Time.unscaledTime >= nextRescanTime || bubbleCanvasTransform == null || cachedBubbleTexts.Length == 0)
        {
            RefreshSceneReferences(forceRefreshFont: false);
        }

        EnsureBubbleIsReadable();
    }

    void RefreshSceneReferences(bool forceRefreshFont)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bubbleCanvasTransform = null;
        chatScreenTransform = null;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || canvas.gameObject.scene != activeScene || canvas.renderMode != RenderMode.WorldSpace)
            {
                continue;
            }

            Transform chatScreen = canvas.transform.Find("ChatScreen");
            if (chatScreen == null)
            {
                continue;
            }

            bubbleCanvasTransform = canvas.transform;
            chatScreenTransform = chatScreen as RectTransform;
            break;
        }

        List<TMP_Text> bubbleTexts = new List<TMP_Text>();
        List<TMP_Text> worldBubbleUiTexts = new List<TMP_Text>();
        TMP_Text[] textComponents = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_Text textComponent in textComponents)
        {
            if (textComponent == null || textComponent.gameObject.scene != activeScene)
            {
                continue;
            }

            if (!BelongsToWorldBubble(textComponent.transform))
            {
                continue;
            }

            worldBubbleUiTexts.Add(textComponent);

            if (string.Equals(textComponent.gameObject.name, BubbleTextObjectName, StringComparison.Ordinal))
            {
                bubbleTexts.Add(textComponent);
            }
        }

        cachedBubbleTexts = bubbleTexts.ToArray();
        cachedWorldBubbleUiTexts = worldBubbleUiTexts.ToArray();
        nextRescanTime = Time.unscaledTime + RescanIntervalSeconds;

        ApplyReadableFont();
    }

    void ApplyReadableFont()
    {
        TMP_FontAsset fontAsset = GetReadableFontAsset();

        if (fontAsset == null)
        {
            return;
        }

        Material fontMaterial = fontAsset.material;
        foreach (TMP_Text bubbleUiText in cachedWorldBubbleUiTexts)
        {
            if (bubbleUiText == null)
            {
                continue;
            }

            bubbleUiText.font = fontAsset;
            if (fontMaterial != null)
            {
                bubbleUiText.fontSharedMaterial = fontMaterial;
            }

            if (string.Equals(bubbleUiText.gameObject.name, BubbleTextObjectName, StringComparison.Ordinal))
            {
                bubbleUiText.fontSize = 82f;
                bubbleUiText.enableWordWrapping = true;
                bubbleUiText.overflowMode = TextOverflowModes.Ellipsis;
            }
            else if (string.Equals(bubbleUiText.gameObject.name, SpeakerTextObjectName, StringComparison.Ordinal))
            {
                bubbleUiText.fontSize = 52f;
            }

            bubbleUiText.SetAllDirty();
            bubbleUiText.ForceMeshUpdate();
        }
    }

    TMP_FontAsset GetReadableFontAsset()
    {
        TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;
        if (fontAsset != null)
        {
            return fontAsset;
        }

        fontAsset = Resources.Load<TMP_FontAsset>("TextMesh Pro/Fonts & Materials/LiberationSans SDF");
        if (fontAsset != null)
        {
            return fontAsset;
        }

        foreach (TMP_Text bubbleText in cachedBubbleTexts)
        {
            if (bubbleText != null && bubbleText.font != null)
            {
                return bubbleText.font;
            }
        }

        return null;
    }

    bool BelongsToWorldBubble(Transform target)
    {
        Canvas worldCanvas = target.GetComponentInParent<Canvas>(true);
        if (worldCanvas == null || worldCanvas.renderMode != RenderMode.WorldSpace)
        {
            return false;
        }

        return worldCanvas.transform.Find("ChatScreen") != null;
    }

    void EnsureBubbleIsReadable()
    {
        if (bubbleCanvasTransform != null)
        {
            bubbleCanvasTransform.localScale = Vector3.one * TargetBubbleCanvasScale;

            RectTransform canvasRect = bubbleCanvasTransform as RectTransform;
            if (canvasRect != null)
            {
                Vector2 anchoredPosition = canvasRect.anchoredPosition;
                anchoredPosition.y = MinimumBubbleHeight;
                canvasRect.anchoredPosition = anchoredPosition;
            }
        }

        if (chatScreenTransform != null)
        {
            chatScreenTransform.localScale = Vector3.one * TargetChatScreenScale;
        }
    }

    float LargestScaleComponent(Vector3 scale)
    {
        return Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
    }
}
