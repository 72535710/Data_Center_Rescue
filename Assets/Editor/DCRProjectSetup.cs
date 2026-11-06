#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;

namespace DataCenterRescue.Editor
{
    public static class DCRProjectSetup
    {
        private const string PlayableScene = "Assets/Scenes/DataCenter_Demo.unity";
        private const string RuntimeFontAsset = "Assets/DCRRuntimeUI/Resources/DCRLiberationSans.ttf";

        [MenuItem("Data Center Rescue/Set Playable Scene as Startup")]
        public static void SetPlayableSceneAsStartup()
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayableScene))
            {
                Debug.LogError("No se encontró la escena: " + PlayableScene);
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(PlayableScene, true) };
            PlayerSettings.productName = "Data Center Rescue";
            PlayerSettings.companyName = "Data Center Rescue Project";
            PlayerSettings.bundleVersion = "1.6.0";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("Data Center Rescue: DataCenter_Demo quedó configurada como escena de inicio.");
        }


        [MenuItem("Data Center Rescue/Enable OpenXR for PC + Android")]
        public static void EnableOpenXRForTargets()
        {
            string settingsFolder = "Assets/XR";
            if (!AssetDatabase.IsValidFolder(settingsFolder))
                AssetDatabase.CreateFolder("Assets", "XR");

            XRGeneralSettingsPerBuildTarget buildTargetSettings;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out buildTargetSettings))
            {
                buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(buildTargetSettings, settingsFolder + "/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, buildTargetSettings, true);
            }

            EnableOpenXRForGroup(buildTargetSettings, BuildTargetGroup.Standalone);
            EnableOpenXRForGroup(buildTargetSettings, BuildTargetGroup.Android);
            EditorUtility.SetDirty(buildTargetSettings);
            AssetDatabase.SaveAssets();
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
            Debug.Log("Data Center Rescue: se solicitó habilitar OpenXR en Standalone y Android. Revisa XR Plug-in Management y los perfiles de interacción del visor.");
        }

        private static void EnableOpenXRForGroup(XRGeneralSettingsPerBuildTarget buildTargetSettings, BuildTargetGroup group)
        {
            if (buildTargetSettings.SettingsForBuildTarget(group) == null)
                buildTargetSettings.CreateDefaultSettingsForBuildTarget(group);

            XRGeneralSettings generalSettings = buildTargetSettings.SettingsForBuildTarget(group);
            if (generalSettings == null)
            {
                Debug.LogWarning("No se pudieron crear ajustes XR para " + group + ". Activa OpenXR manualmente desde Project Settings.");
                return;
            }

            if (generalSettings.Manager == null)
                buildTargetSettings.CreateDefaultManagerSettingsForBuildTarget(group);

            generalSettings = buildTargetSettings.SettingsForBuildTarget(group);
            if (generalSettings == null || generalSettings.Manager == null)
            {
                Debug.LogWarning("No se encontró XRManagerSettings para " + group + ". Activa OpenXR manualmente.");
                return;
            }

            generalSettings.InitManagerOnStart = true;
            bool assigned = XRPackageMetadataStore.AssignLoader(
                generalSettings.Manager,
                "UnityEngine.XR.OpenXR.OpenXRLoader",
                group);
            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(generalSettings.Manager);
            if (!assigned)
                Debug.LogWarning("OpenXR ya estaba asignado o no pudo asignarse automáticamente para " + group + ". Comprueba el listado de proveedores XR.");
        }

        [MenuItem("Data Center Rescue/Open XR Plug-in Settings")]
        public static void OpenXRSettings()
        {
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
        }

        /// <summary>
        /// Called by Tools/compile_check.ps1 after Unity has imported the project.
        /// The method can only run if all Editor and runtime assemblies compile successfully.
        /// </summary>
        public static void RunBatchCompileCheck()
        {
            SceneAsset playableScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayableScene);
            if (playableScene == null)
            {
                Debug.LogError("DCR_BATCH_COMPILE_VALIDATION_FAILED: playable scene missing: " + PlayableScene);
                EditorApplication.Exit(2);
                return;
            }

            Font resourceFont = Resources.Load<Font>("DCRLiberationSans");
            Material defaultCanvasMaterial = Canvas.GetDefaultCanvasMaterial();
            Shader uiShader = defaultCanvasMaterial != null ? defaultCanvasMaterial.shader : null;
            if (resourceFont == null || defaultCanvasMaterial == null || uiShader == null || !uiShader.isSupported)
            {
                Debug.LogError("DCR_BATCH_COMPILE_VALIDATION_FAILED: bundled UGUI font or default Canvas material is missing/unsupported.");
                EditorApplication.Exit(3);
                return;
            }

            GameObject cameraObject = new GameObject("DCR_UI_ProbeCamera", typeof(Camera));
            Camera probeCamera = cameraObject.GetComponent<Camera>();
            probeCamera.enabled = false;
            probeCamera.nearClipPlane = 0.08f;
            UniversalAdditionalCameraData baseCameraData = probeCamera.GetUniversalAdditionalCameraData();
            baseCameraData.renderType = CameraRenderType.Base;
            List<Camera> cameraStack = baseCameraData.cameraStack;

            GameObject overlayCameraObject = new GameObject("DCR_UI_ProbeOverlayCamera", typeof(Camera));
            overlayCameraObject.transform.SetParent(cameraObject.transform, false);
            Camera overlayCamera = overlayCameraObject.GetComponent<Camera>();
            overlayCamera.CopyFrom(probeCamera);
            overlayCamera.transform.localPosition = Vector3.zero;
            overlayCamera.transform.localRotation = Quaternion.identity;
            overlayCamera.targetTexture = null;
            overlayCamera.enabled = false;
            overlayCamera.cullingMask = 1 << DCRUIStyle.UILayer;
            overlayCamera.clearFlags = CameraClearFlags.Nothing;
            overlayCamera.nearClipPlane = 0.01f;
            overlayCamera.farClipPlane = 10f;
            UniversalAdditionalCameraData overlayCameraData = overlayCamera.GetUniversalAdditionalCameraData();
            overlayCameraData.renderType = CameraRenderType.Overlay;
            overlayCameraData.allowXRRendering = true;
            overlayCameraData.renderPostProcessing = false;
            overlayCameraData.renderShadows = false;
            overlayCameraData.volumeLayerMask = 0;
            if (cameraStack != null)
                cameraStack.Add(overlayCamera);

            Canvas canvasProbe = DCRUIStyle.CreateScreenCanvas(probeCamera, false);
            GameObject probeObject = new GameObject("DCR_UI_BatchProbe",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            probeObject.transform.SetParent(canvasProbe.transform, false);
            probeObject.layer = DCRUIStyle.UILayer;
            Text probeText = probeObject.GetComponent<Text>();
            probeText.font = resourceFont;
            probeText.material = defaultCanvasMaterial;
            probeText.fontSize = 42;
            probeText.text = "DATA CENTER RESCUE 0123456789 ÁÉÍÓÚ Ñ áéíóú ñ ¿¡";
            probeText.resizeTextForBestFit = true;
            probeText.resizeTextMinSize = 18;
            probeText.resizeTextMaxSize = 42;
            probeText.color = Color.white;
            probeText.alignment = TextAnchor.MiddleCenter;
            probeText.rectTransform.anchorMin = new Vector2(0f, 0f);
            probeText.rectTransform.anchorMax = new Vector2(1f, 1f);
            probeText.rectTransform.offsetMin = new Vector2(24f, 24f);
            probeText.rectTransform.offsetMax = new Vector2(-24f, -24f);
            resourceFont.RequestCharactersInTexture(probeText.text, probeText.fontSize, probeText.fontStyle);
            probeText.SetVerticesDirty();
            probeText.SetLayoutDirty();
            Canvas.ForceUpdateCanvases();
            TextGenerator generator = probeText.cachedTextGenerator;
            TextGenerationSettings generationSettings = probeText.GetGenerationSettings(new Vector2(1872f, 1032f));
            bool generated = generator.Populate(probeText.text, generationSettings);
            int visibleCharacters = generator.characterCountVisible;
            Material renderedMaterial = probeText.canvasRenderer.GetMaterial();
            Material effectiveMaterial = probeText.materialForRendering;
            bool materialReady = renderedMaterial != null && renderedMaterial.shader == uiShader &&
                                 effectiveMaterial != null && effectiveMaterial.shader == uiShader &&
                                 probeText.canvasRenderer.materialCount > 0 && uiShader.isSupported &&
                                 probeText.mainTexture != null;
            bool desktopCanvasReady = canvasProbe.renderMode == RenderMode.ScreenSpaceOverlay &&
                                      canvasProbe.sortingOrder == DCRUIStyle.CanvasSortOrder &&
                                      canvasProbe.GetComponent<GraphicRaycaster>() != null;

            // Exercise the same menu primitives used by the runtime: a scrollable
            // body clipped to its viewport, plus a raycastable button with its label.
            RectTransform scrollViewport = DCRUIStyle.CreateImage(canvasProbe.transform,
                "DCR_UI_BatchScrollViewport", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0f)).GetComponent<RectTransform>();
            scrollViewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scrollProbe = scrollViewport.gameObject.AddComponent<ScrollRect>();
            scrollProbe.viewport = scrollViewport;
            scrollProbe.horizontal = false;
            scrollProbe.vertical = true;
            scrollProbe.scrollSensitivity = 30f;
            GameObject contentObject = new GameObject("DCR_UI_BatchScrollContent", typeof(RectTransform));
            contentObject.layer = DCRUIStyle.UILayer;
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.SetParent(scrollViewport, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 1600f);
            scrollProbe.content = contentRect;

            RectTransform buttonRect = DCRUIStyle.CreateImage(canvasProbe.transform,
                "DCR_UI_BatchButton", new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.2f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0.045f, 0.16f, 0.22f, 1f), true).GetComponent<RectTransform>();
            Image buttonImage = buttonRect.GetComponent<Image>();
            DCRWorldButton buttonProbe = buttonRect.gameObject.AddComponent<DCRWorldButton>();
            buttonProbe.Configure(null, 0, buttonImage, "VOLVER");
            Text buttonLabel = DCRUIStyle.CreateText(buttonRect, "DCR_UI_BatchButtonLabel", "VOLVER",
                DCRTextRole.PanelButton, Color.white, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));
            bool menuFlowReady = scrollProbe.viewport == scrollViewport && scrollProbe.content == contentRect &&
                                 !scrollProbe.horizontal && scrollProbe.vertical &&
                                 Mathf.Approximately(scrollProbe.scrollSensitivity, 30f) &&
                                 buttonImage.raycastTarget && buttonLabel != null &&
                                 buttonLabel.transform.parent == buttonRect;

            DCRUIStyle.ConfigureScreenCanvas(canvasProbe, overlayCamera, true);
            bool xrCanvasReady = canvasProbe.renderMode == RenderMode.ScreenSpaceCamera &&
                                 canvasProbe.worldCamera == overlayCamera &&
                                 Mathf.Approximately(canvasProbe.planeDistance, DCRUIStyle.XRCanvasPlaneDistance) &&
                                 cameraStack != null && cameraStack.Contains(overlayCamera) &&
                                 overlayCameraData.renderType == CameraRenderType.Overlay &&
                                 overlayCamera.cullingMask == (1 << DCRUIStyle.UILayer) &&
                                 overlayCameraData.clearDepth;

            if (cameraStack != null) cameraStack.Remove(overlayCamera);
            Object.DestroyImmediate(canvasProbe.gameObject);
            Object.DestroyImmediate(overlayCameraObject);
            Object.DestroyImmediate(cameraObject);
            DCRUIStyle.ReleaseRuntimeResources();

            if (!generated || visibleCharacters < 20 || !materialReady || !desktopCanvasReady || !xrCanvasReady || !menuFlowReady)
            {
                Debug.LogError("DCR_BATCH_COMPILE_VALIDATION_FAILED: UGUI front-screen render path failed. generated=" + generated +
                    " | visibleCharacters=" + visibleCharacters + " | material=" + materialReady +
                    " | desktopOverlay=" + desktopCanvasReady + " | xrOverlayCameraStack=" + xrCanvasReady + " | menuScrollAndButtons=" + menuFlowReady);
                EditorApplication.Exit(4);
                return;
            }

            string[] scripts = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts", "Assets/Editor" });
            Debug.Log("DCR_BATCH_COMPILE_VALIDATION_PASSED | Unity " + Application.unityVersion +
                      " | Scripts discovered: " + scripts.Length + " | Scene: " + PlayableScene +
                      " | UGUI font: " + resourceFont.name + " | Visible generated characters: " + visibleCharacters +
                      " | Desktop UI: ScreenSpaceOverlay | XR UI: ScreenSpaceCamera + URP Overlay Camera Stack + UI-only culling + depth clear | ScrollRect: OK | button label/raycast: OK | GraphicRaycaster: OK");
        }

    }
}
#endif
