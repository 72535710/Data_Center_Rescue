using UnityEngine;
using UnityEngine.UI;

namespace DataCenterRescue
{
    public sealed class DCRWorldButton : MonoBehaviour
    {
        public int Index { get; private set; }
        public string ActionLabel { get; private set; }
        public DCRGameManager Manager { get; private set; }
        public Image ButtonImage { get; private set; }

        private static readonly Color NormalColor = new Color(0.045f, 0.16f, 0.22f, 1f);
        private static readonly Color HoverColor = new Color(0.06f, 0.46f, 0.60f, 1f);
        private static readonly Color SelectedColor = new Color(0.075f, 0.31f, 0.41f, 1f);
        private bool isHovered;
        private bool isSelected;

        public void Configure(DCRGameManager manager, int index, Image image, string actionLabel)
        {
            Manager = manager;
            Index = index;
            ButtonImage = image;
            ActionLabel = actionLabel ?? string.Empty;
            if (ButtonImage != null) ButtonImage.raycastTarget = true;
            RefreshColor();
        }

        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            RefreshColor();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            RefreshColor();
        }

        private void RefreshColor()
        {
            if (ButtonImage == null) return;
            ButtonImage.color = isHovered ? HoverColor : (isSelected ? SelectedColor : NormalColor);
        }
    }

    public sealed class DCRMaintenanceAction : MonoBehaviour
    {
        public int ActionIndex { get; private set; }
        public Renderer ActionRenderer { get; private set; }
        public bool IsCompleted { get; private set; }
        public string ActionTitle { get; private set; }
        public int CompletedSteps { get; private set; }

        private Renderer[] progressRenderers;
        private bool isHovered;

        public void Configure(int actionIndex, string actionTitle, Renderer actionRenderer,
            Renderer[] stepIndicators)
        {
            ActionIndex = actionIndex;
            ActionTitle = actionTitle;
            ActionRenderer = actionRenderer;
            progressRenderers = stepIndicators;
            CompletedSteps = 0;
            RefreshColor();
        }

        public void SetProgress(int completedSteps, int totalSteps)
        {
            CompletedSteps = Mathf.Clamp(completedSteps, 0, Mathf.Max(1, totalSteps));
            RefreshColor();
        }

        public void SetCompleted(bool completed)
        {
            IsCompleted = completed;
            if (completed && progressRenderers != null)
                CompletedSteps = progressRenderers.Length;
            RefreshColor();
        }

        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            RefreshColor();
        }

        private void RefreshColor()
        {
            if (ActionRenderer != null)
            {
                if (IsCompleted)
                    ActionRenderer.material.color = new Color(0.10f, 0.42f, 0.22f, 1f);
                else if (isHovered)
                    ActionRenderer.material.color = new Color(0.08f, 0.62f, 0.78f, 1f);
                else if (CompletedSteps > 0)
                    ActionRenderer.material.color = new Color(0.42f, 0.30f, 0.08f, 1f);
                else
                    ActionRenderer.material.color = new Color(0.06f, 0.20f, 0.27f, 1f);
            }

            if (progressRenderers == null) return;
            for (int i = 0; i < progressRenderers.Length; i++)
            {
                Renderer indicator = progressRenderers[i];
                if (indicator == null) continue;
                indicator.material.color = i < CompletedSteps
                    ? new Color(0.14f, 0.88f, 0.46f, 1f)
                    : new Color(0.10f, 0.17f, 0.20f, 1f);
            }
        }
    }

    public sealed class DCRStation : MonoBehaviour
    {
        public int MissionIndex { get; private set; }
        public string StationName { get; private set; }
        public Renderer MainRenderer { get; private set; }
        public Renderer ScreenRenderer { get; private set; }
        public Renderer BeaconRenderer { get; private set; }
        public bool IsCurrent { get; private set; }
        public bool IsCompleted { get; private set; }

        private Color baseColor = new Color(0.045f, 0.16f, 0.22f, 1f);
        private Color activeColor = new Color(0.04f, 0.48f, 0.58f, 1f);
        private Color completedColor = new Color(0.12f, 0.34f, 0.22f, 1f);

        public void Configure(int missionIndex, string stationName, Renderer mainRenderer,
            Renderer screenRenderer, Renderer beaconRenderer)
        {
            MissionIndex = missionIndex;
            StationName = stationName;
            MainRenderer = mainRenderer;
            ScreenRenderer = screenRenderer;
            BeaconRenderer = beaconRenderer;
            SetState(false, false);
        }

        public void SetMissionIndex(int missionIndex)
        {
            MissionIndex = missionIndex;
        }

        public void SetState(bool current, bool completed)
        {
            IsCurrent = current;
            IsCompleted = completed;

            Color color = current ? activeColor : (completed ? completedColor : baseColor);
            if (MainRenderer != null) MainRenderer.material.color = color;
            if (ScreenRenderer != null)
                ScreenRenderer.material.color = current
                    ? new Color(0.05f, 0.78f, 0.92f, 1f)
                    : (completed ? new Color(0.2f, 0.68f, 0.36f, 1f) : new Color(0.035f, 0.09f, 0.12f, 1f));
            if (BeaconRenderer != null)
                BeaconRenderer.material.color = current
                    ? new Color(0.1f, 0.95f, 1f, 1f)
                    : (completed ? new Color(0.2f, 0.9f, 0.45f, 1f) : new Color(0.16f, 0.2f, 0.22f, 1f));
        }
    }

    public sealed class DCRBillboard : MonoBehaviour
    {
        private Camera targetCamera;
        private Transform anchor;
        private Vector3 anchorLocalPosition;
        private float cameraFacingOffset = 0.10f;

        public void Configure(Camera cameraReference, Transform anchorTransform,
            Vector3 localAnchorPosition, float offsetTowardCamera)
        {
            targetCamera = cameraReference;
            anchor = anchorTransform;
            anchorLocalPosition = localAnchorPosition;
            cameraFacingOffset = Mathf.Max(0f, offsetTowardCamera);
        }

        private void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            Vector3 basePosition = anchor != null
                ? anchor.TransformPoint(anchorLocalPosition)
                : transform.position;
            Vector3 towardCamera = targetCamera.transform.position - basePosition;
            towardCamera.y = 0f;
            if (towardCamera.sqrMagnitude > 0.0001f && anchor != null)
                transform.position = basePosition + towardCamera.normalized * cameraFacingOffset;
            else if (anchor != null)
                transform.position = basePosition;

            // The label stays camera-facing and is nudged slightly out from the
            // terminal/rack plane so nearby equipment does not z-occlude its text.
            Vector3 direction = transform.position - targetCamera.transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Vector3 forward = direction.normalized;
                Vector3 upReference = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f
                    ? Vector3.forward
                    : Vector3.up;
                transform.rotation = Quaternion.LookRotation(forward, upReference);
            }
        }
    }
}
