using UnityEngine;
using UnityEngine.UI;

namespace DataCenterRescue
{
    public enum DCRTextRole
    {
        PanelTitle,
        PanelBody,
        PanelButton,
        Hud,
        Prompt,
        Reticle,
        WorldLabel
    }

    /// <summary>
    /// Centralized screen UI. Desktop uses Screen Space Overlay. XR uses a Screen Space Camera
    /// Canvas rendered by a dedicated URP Overlay camera in the base camera stack. That camera
    /// renders only the UI layer and clears the scene depth, so world geometry cannot occlude UI.
    /// CanvasScaler and normalized anchors keep the interface fitted to different displays.
    /// </summary>
    public static class DCRUIStyle
    {
        public const int ReferenceWidth = 1920;
        public const int ReferenceHeight = 1080;
        public const int CanvasSortOrder = 32760;
        public const int UILayer = 5;
        // XR uses a dedicated URP Overlay camera with a cleared depth buffer.
        // A 1.2 m canvas distance is comfortable to read and cannot be depth-occluded.
        public const float XRCanvasPlaneDistance = 1.2f;
        public const float XRCameraNearClip = 0.08f;

        private const string RuntimeFontPath = "DCRLiberationSans";
        private static Font cachedRuntimeFont;
        private static Material sharedTextMaterial;
        private static bool fontErrorLogged;
        private static bool materialErrorLogged;

        public static Font GetRuntimeFont()
        {
            if (cachedRuntimeFont != null) return cachedRuntimeFont;

            cachedRuntimeFont = Resources.Load<Font>(RuntimeFontPath);
            if (cachedRuntimeFont == null)
                cachedRuntimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (cachedRuntimeFont == null)
                cachedRuntimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (cachedRuntimeFont == null && !fontErrorLogged)
            {
                fontErrorLogged = true;
                Debug.LogError("DCR_UI_FATAL: no se encontró DCRLiberationSans.ttf ni una fuente integrada de Unity.");
            }
            return cachedRuntimeFont;
        }

        public static Material GetRuntimeTextMaterial()
        {
            if (sharedTextMaterial == null)
                sharedTextMaterial = Canvas.GetDefaultCanvasMaterial();

            if (sharedTextMaterial == null && !materialErrorLogged)
            {
                materialErrorLogged = true;
                Debug.LogError("DCR_UI_FATAL: Unity no devolvió el material UGUI predeterminado.");
            }
            return sharedTextMaterial;
        }

        public static Canvas CreateScreenCanvas(Camera camera, bool xrActive)
        {
            GameObject canvasObject = new GameObject("DCR_ScreenUI",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = UILayer;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            ConfigureScreenCanvas(canvas, camera, xrActive);

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 1f;

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            raycaster.ignoreReversedGraphics = true;
            return canvas;
        }

        public static void ConfigureScreenCanvas(Canvas canvas, Camera camera, bool xrActive)
        {
            if (canvas == null) return;
            canvas.overrideSorting = true;
            canvas.sortingOrder = CanvasSortOrder;
            canvas.pixelPerfect = false;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
            if (xrActive && camera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = XRCanvasPlaneDistance;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
        }

        public static RectTransform CreateImage(Transform parent, string objectName,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 size, Vector2 anchoredPosition, Color color, bool raycastTarget = false)
        {
            GameObject imageObject = new GameObject(objectName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.layer = UILayer;
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            image.maskable = false;
            image.material = GetRuntimeTextMaterial();
            return rect;
        }

        public static Text CreateText(Transform parent, string objectName, string value,
            DCRTextRole role, Color color, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject textObject = new GameObject(objectName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.layer = UILayer;
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Text text = textObject.GetComponent<Text>();
            Apply(text, value, role, color, alignment);
            return text;
        }

        public static void Apply(Text text, string value, DCRTextRole role,
            Color color, TextAnchor alignment)
        {
            if (text == null) return;
            int maxSize;
            int minSize;
            switch (role)
            {
                case DCRTextRole.PanelTitle: maxSize = 50; minSize = 34; break;
                case DCRTextRole.PanelBody: maxSize = 29; minSize = 19; break;
                case DCRTextRole.PanelButton: maxSize = 30; minSize = 19; break;
                case DCRTextRole.Hud: maxSize = 25; minSize = 18; break;
                case DCRTextRole.Prompt: maxSize = 26; minSize = 18; break;
                case DCRTextRole.Reticle: maxSize = 40; minSize = 32; break;
                default: maxSize = 23; minSize = 17; break;
            }

            Font font = GetRuntimeFont();
            if (font != null) text.font = font;
            text.material = GetRuntimeTextMaterial();
            text.text = value ?? string.Empty;
            text.fontSize = maxSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = true;
            text.alignByGeometry = true;
            text.lineSpacing = 1.05f;
            text.raycastTarget = false;
            text.maskable = false;
            text.canvasRenderer.cullTransparentMesh = false;
            text.canvasRenderer.SetAlpha(1f);
            text.enabled = true;
            text.gameObject.SetActive(true);
            text.SetVerticesDirty();
            text.SetLayoutDirty();
            text.SetMaterialDirty();
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
                children[i].gameObject.layer = layer;
        }

        public static void ReleaseRuntimeResources()
        {
            sharedTextMaterial = null;
            cachedRuntimeFont = null;
        }
    }
}
