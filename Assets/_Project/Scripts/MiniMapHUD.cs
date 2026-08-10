using UnityEngine;
using UnityEngine.UI;

namespace ZombieInfinite
{
    [DisallowMultipleComponent]
    public sealed class MiniMapHUD : MonoBehaviour
    {
        private const string TerrainLayerName = "MiniMapTerrain";
        private const string VisibleLayerName = "MiniMapVisible";
        private const string PlayerMarkerName = "MiniMap Player Icon";

        [Header("Scene References")]
        [Tooltip("Player được gán trực tiếp trong scene.")]
        [SerializeField] private Transform player;

        [Tooltip("Camera gameplay dùng để xác định hướng phía trên của minimap.")]
        [SerializeField] private Camera directionCamera;

        [Tooltip("Camera minimap đặt sẵn trong scene. Chỉ render MiniMapTerrain + MiniMapVisible.")]
        [SerializeField] private Camera miniMapCamera;

        [Tooltip("Bật: hướng nhìn Camera gameplay luôn ở phía trên minimap. Tắt: hướng trước Player ở phía trên.")]
        [SerializeField] private bool useCameraDirection = true;

        [Header("Scene-authored UI")]
        [Tooltip("RectTransform vùng hiển thị minimap.")]
        [SerializeField] private RectTransform mapContent;

        [Tooltip("Icon Player UI cũ. Chỉ giữ làm fallback nếu world marker chưa được editor setup.")]
        [SerializeField] private Image playerIcon;

        [Header("Map Settings")]
        [SerializeField, Min(5f)] private float worldRadius = 30f;
        [SerializeField, Min(1f)] private float cameraHeight = 50f;
        [SerializeField, Range(128, 1024)] private int renderTextureResolution = 512;
        [SerializeField] private Color emptyMapColor = new(0.04f, 0.05f, 0.04f, 1f);

        private readonly Vector3[] viewportCorners = new Vector3[4];
        private GameMenuController gameMenu;
        private RawImage mapDisplay;
        private RenderTexture mapRenderTexture;
        private bool usingRenderTexture;

        private void Awake()
        {
            if (!ValidateSceneReferences())
            {
                enabled = false;
                return;
            }

            gameMenu = player.GetComponent<GameMenuController>();
            mapDisplay = mapContent.GetComponent<RawImage>();

            ConfigureCameraLayers();
            ConfigureMiniMapCamera();
            ConfigureDisplayMode();
            ConfigurePlayerIconFallback();
        }

        private void LateUpdate()
        {
            bool visible = gameMenu == null || !gameMenu.GameplayBlocked;
            SetVisible(visible);
            if (!visible)
            {
                return;
            }

            UpdateCamera(GetHeadingForward());

            // Nếu scene chưa kịp có RawImage thì Camera vẫn render trực tiếp
            // đúng vào vùng MapContent, không để minimap bị trống.
            if (!usingRenderTexture)
            {
                UpdateCameraViewport();
                UpdateFallbackPlayerIcon();
            }
        }

        private bool ValidateSceneReferences()
        {
            bool valid = true;

            if (player == null)
            {
                Debug.LogError("MiniMapHUD requires Player assigned in the scene.", this);
                valid = false;
            }

            if (directionCamera == null)
            {
                Debug.LogError("MiniMapHUD requires Direction Camera assigned in the scene.", this);
                valid = false;
            }

            if (miniMapCamera == null)
            {
                Debug.LogError("MiniMapHUD requires MiniMapCamera assigned in the scene.", this);
                valid = false;
            }

            if (mapContent == null)
            {
                Debug.LogError("MiniMapHUD requires MapContent assigned in the scene.", this);
                valid = false;
            }

            return valid;
        }

        private void ConfigureCameraLayers()
        {
            int terrainLayer = LayerMask.NameToLayer(TerrainLayerName);
            int visibleLayer = LayerMask.NameToLayer(VisibleLayerName);

            if (terrainLayer < 0 || visibleLayer < 0)
            {
                Debug.LogError(
                    $"MiniMapHUD requires layers '{TerrainLayerName}' and '{VisibleLayerName}'.",
                    this);
                return;
            }

            int terrainMask = 1 << terrainLayer;
            int visibleMask = 1 << visibleLayer;

            // Minimap chỉ thấy Terrain và marker/icon minimap.
            miniMapCamera.cullingMask = terrainMask | visibleMask;

            // Camera gameplay không thấy icon minimap.
            directionCamera.cullingMask &= ~visibleMask;
        }

        private void ConfigureMiniMapCamera()
        {
            miniMapCamera.orthographic = true;
            miniMapCamera.orthographicSize = worldRadius;
            miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
            miniMapCamera.backgroundColor = emptyMapColor;
        }

        private void ConfigureDisplayMode()
        {
            usingRenderTexture = mapDisplay != null;

            if (usingRenderTexture)
            {
                miniMapCamera.rect = new Rect(0f, 0f, 1f, 1f);
                CreateRenderTexture();
                return;
            }

            // Fallback an toàn: không tạo UI component runtime.
            // Camera render trực tiếp vào rect của MapContent.
            ReleaseRenderTexture();
            miniMapCamera.targetTexture = null;
            Debug.LogWarning(
                "MiniMap MapContent chưa có RawImage. Đang dùng camera viewport fallback; editor setup sẽ tự thêm RawImage và lưu scene.",
                this);
        }

        private void ConfigurePlayerIconFallback()
        {
            if (playerIcon == null)
            {
                return;
            }

            bool hasWorldMarker = player.Find(PlayerMarkerName) != null;
            playerIcon.enabled = !hasWorldMarker;
        }

        private void CreateRenderTexture()
        {
            ReleaseRenderTexture();

            int resolution = Mathf.Clamp(renderTextureResolution, 128, 1024);
            var descriptor = new RenderTextureDescriptor(
                resolution,
                resolution,
                RenderTextureFormat.ARGB32,
                16)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false
            };

            mapRenderTexture = new RenderTexture(descriptor)
            {
                name = "MiniMap RenderTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            mapRenderTexture.Create();

            miniMapCamera.targetTexture = mapRenderTexture;
            mapDisplay.texture = mapRenderTexture;
            mapDisplay.color = Color.white;
            mapDisplay.raycastTarget = false;
        }

        private void ReleaseRenderTexture()
        {
            if (miniMapCamera != null && miniMapCamera.targetTexture == mapRenderTexture)
            {
                miniMapCamera.targetTexture = null;
            }

            if (mapDisplay != null && mapDisplay.texture == mapRenderTexture)
            {
                mapDisplay.texture = null;
            }

            if (mapRenderTexture == null)
            {
                return;
            }

            if (mapRenderTexture.IsCreated())
            {
                mapRenderTexture.Release();
            }

            Destroy(mapRenderTexture);
            mapRenderTexture = null;
        }

        private void SetVisible(bool visible)
        {
            if (mapContent.gameObject.activeSelf != visible)
            {
                mapContent.gameObject.SetActive(visible);
            }

            if (miniMapCamera.enabled != visible)
            {
                miniMapCamera.enabled = visible;
            }
        }

        private void UpdateCamera(Vector3 headingForward)
        {
            float headingYaw = Mathf.Atan2(headingForward.x, headingForward.z) * Mathf.Rad2Deg;
            Vector3 position = player.position + Vector3.up * cameraHeight;
            Quaternion rotation = Quaternion.Euler(90f, headingYaw, 0f);

            miniMapCamera.transform.SetPositionAndRotation(position, rotation);
            miniMapCamera.orthographicSize = worldRadius;
        }

        private void UpdateCameraViewport()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            mapContent.GetWorldCorners(viewportCorners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, viewportCorners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, viewportCorners[2]);

            float x = Mathf.Clamp01(bottomLeft.x / Screen.width);
            float y = Mathf.Clamp01(bottomLeft.y / Screen.height);
            float right = Mathf.Clamp01(topRight.x / Screen.width);
            float top = Mathf.Clamp01(topRight.y / Screen.height);

            miniMapCamera.rect = new Rect(
                x,
                y,
                Mathf.Max(0f, right - x),
                Mathf.Max(0f, top - y));
        }

        private void UpdateFallbackPlayerIcon()
        {
            if (playerIcon == null || !playerIcon.enabled)
            {
                return;
            }

            RectTransform iconRect = playerIcon.rectTransform;
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.localRotation = Quaternion.identity;
        }

        private Vector3 GetHeadingForward()
        {
            Vector3 forward = useCameraDirection
                ? directionCamera.transform.forward
                : player.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            return forward.normalized;
        }

        private void OnDisable()
        {
            if (miniMapCamera != null)
            {
                miniMapCamera.enabled = false;
            }
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture();
        }

        private void OnValidate()
        {
            worldRadius = Mathf.Max(5f, worldRadius);
            cameraHeight = Mathf.Max(1f, cameraHeight);
            renderTextureResolution = Mathf.Clamp(renderTextureResolution, 128, 1024);

            if (miniMapCamera != null && directionCamera != null)
            {
                ConfigureCameraLayers();
                ConfigureMiniMapCamera();
            }
        }
    }
}
