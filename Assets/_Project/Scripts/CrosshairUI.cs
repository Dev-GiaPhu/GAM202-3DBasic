using UnityEngine;
using UnityEngine.UI;

namespace ZombieInfinite
{
    public sealed class CrosshairUI : MonoBehaviour
    {
        [Header("Crosshair")]
        [SerializeField, Min(1f)]
        private float lineLength = 9f;

        [SerializeField, Min(1f)]
        private float lineThickness = 2f;

        [SerializeField, Min(0f)]
        private float gap = 4f;

        [SerializeField, Min(1f)]
        private float centerDotSize = 3f;

        [SerializeField]
        private Color crosshairColor = Color.white;

        [Header("Aim Down Sights")]
        [SerializeField] private Color aimingColor = Color.red;
        [SerializeField, Min(1f)] private float aimingScale = 1.45f;
        [SerializeField, Min(0.1f)] private float aimingTransitionSpeed = 10f;

        private Canvas canvas;
        private RectTransform crosshairRoot;
        private RawImage[] crosshairParts;
        private TopDownCameraFollow cameraFollow;
        private float aimBlend;

        private void Awake()
        {
            BuildCrosshair();
            cameraFollow = FindFirstObjectByType<TopDownCameraFollow>();
        }

        private void Update()
        {
            if (crosshairRoot == null)
            {
                return;
            }

            if (cameraFollow == null)
            {
                cameraFollow = FindFirstObjectByType<TopDownCameraFollow>();
            }

            float target = cameraFollow != null && cameraFollow.IsAiming ? 1f : 0f;
            float blend = 1f - Mathf.Exp(
                -aimingTransitionSpeed * Time.unscaledDeltaTime);
            aimBlend = Mathf.Lerp(aimBlend, target, blend);
            float scale = Mathf.Lerp(1f, aimingScale, aimBlend);
            crosshairRoot.localScale = Vector3.one * scale;

            Color color = Color.Lerp(crosshairColor, aimingColor, aimBlend);
            if (crosshairParts == null)
            {
                crosshairParts = crosshairRoot.GetComponentsInChildren<RawImage>(true);
            }

            foreach (RawImage part in crosshairParts)
            {
                part.color = color;
            }
        }

        private void BuildCrosshair()
        {
            canvas = GetComponent<Canvas>();

            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            if (GetComponent<CanvasScaler>() == null)
            {
                CanvasScaler scaler =
                    gameObject.AddComponent<CanvasScaler>();

                scaler.uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;

                scaler.referenceResolution =
                    new Vector2(1920f, 1080f);

                scaler.matchWidthOrHeight = 0.5f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            Transform oldCrosshair =
                transform.Find("RuntimeCrosshair");

            if (oldCrosshair != null)
            {
                Destroy(oldCrosshair.gameObject);
            }

            GameObject rootObject =
                new GameObject(
                    "RuntimeCrosshair",
                    typeof(RectTransform));

            crosshairRoot =
                rootObject.GetComponent<RectTransform>();

            crosshairRoot.SetParent(
                transform,
                false);

            crosshairRoot.anchorMin =
                new Vector2(0.5f, 0.5f);

            crosshairRoot.anchorMax =
                new Vector2(0.5f, 0.5f);

            crosshairRoot.pivot =
                new Vector2(0.5f, 0.5f);

            crosshairRoot.anchoredPosition =
                Vector2.zero;

            crosshairRoot.sizeDelta =
                new Vector2(100f, 100f);

            CreatePart(
                "Top",
                new Vector2(
                    lineThickness,
                    lineLength),
                new Vector2(
                    0f,
                    gap + lineLength * 0.5f));

            CreatePart(
                "Bottom",
                new Vector2(
                    lineThickness,
                    lineLength),
                new Vector2(
                    0f,
                    -(gap + lineLength * 0.5f)));

            CreatePart(
                "Left",
                new Vector2(
                    lineLength,
                    lineThickness),
                new Vector2(
                    -(gap + lineLength * 0.5f),
                    0f));

            CreatePart(
                "Right",
                new Vector2(
                    lineLength,
                    lineThickness),
                new Vector2(
                    gap + lineLength * 0.5f,
                    0f));

            CreatePart(
                "CenterDot",
                new Vector2(
                    centerDotSize,
                    centerDotSize),
                Vector2.zero);

            crosshairParts = crosshairRoot.GetComponentsInChildren<RawImage>(true);
        }

        private void CreatePart(
            string partName,
            Vector2 size,
            Vector2 position)
        {
            GameObject partObject =
                new GameObject(
                    partName,
                    typeof(RectTransform),
                    typeof(RawImage));

            RectTransform rect =
                partObject.GetComponent<RectTransform>();

            rect.SetParent(
                crosshairRoot,
                false);

            rect.anchorMin =
                new Vector2(0.5f, 0.5f);

            rect.anchorMax =
                new Vector2(0.5f, 0.5f);

            rect.pivot =
                new Vector2(0.5f, 0.5f);

            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            RawImage image =
                partObject.GetComponent<RawImage>();

            image.texture = Texture2D.whiteTexture;
            image.color = crosshairColor;
            image.raycastTarget = false;
        }

        private void OnValidate()
        {
            lineLength = Mathf.Max(1f, lineLength);
            lineThickness =
                Mathf.Max(1f, lineThickness);

            gap = Mathf.Max(0f, gap);
            centerDotSize =
                Mathf.Max(1f, centerDotSize);
            aimingScale = Mathf.Max(1f, aimingScale);
            aimingTransitionSpeed = Mathf.Max(0.1f, aimingTransitionSpeed);
        }
    }
}
