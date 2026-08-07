using UnityEngine;
using UnityEngine.UI;

namespace ZombieInfinite
{
    [DisallowMultipleComponent]
    public sealed class MiniMapHUD : MonoBehaviour
    {
        private const string TerrainLayerName = "MiniMapTerrain";
        private const string VisibleLayerName = "MiniMapVisible";

        [Header("Scene References")]
        [Tooltip("Player được gán trực tiếp trong scene.")]
        [SerializeField] private Transform player;

        [Tooltip("Camera gameplay dùng để xác định hướng phía trên của minimap.")]
        [SerializeField] private Camera directionCamera;

        [Tooltip("Camera minimap đặt sẵn trong scene. Script không tạo Camera runtime.")]
        [SerializeField] private Camera miniMapCamera;

        [Tooltip("Bật: hướng nhìn Camera gameplay luôn ở phía trên minimap. Tắt: hướng trước Player ở phía trên.")]
        [SerializeField] private bool useCameraDirection = true;

        [Header("Scene-authored UI")]
        [Tooltip("RectTransform vùng hiển thị map. Camera viewport sẽ bám đúng vùng này.")]
        [SerializeField] private RectTransform mapContent;

        [Tooltip("Icon Player đặt sẵn trong scene.")]
        [SerializeField] private Image playerIcon;

        [Header("Map Settings")]
        [SerializeField, Min(5f)] private float worldRadius = 30f;
        [SerializeField, Min(1f)] private float cameraHeight = 50f;

        private readonly Vector3[] viewportCorners = new Vector3[4];
        private GameMenuController gameMenu;

        private void Awake()
        {
            if (!ValidateSceneReferences())
            {
                enabled = false;
                return;
            }

            gameMenu = player.GetComponent<GameMenuController>();
            ConfigureCameraLayers();
            ConfigureMiniMapCamera();
        }

        private void LateUpdate()
        {
            bool visible = gameMenu == null || !gameMenu.GameplayBlocked;
            SetVisible(visible);
            if (!visible)
            {
                return;
            }

            Vector3 headingForward = GetHeadingForward();
            UpdateCamera(headingForward);
            UpdateCameraViewport();
            UpdatePlayerIcon();
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

            if (playerIcon == null)
            {
                Debug.LogError("MiniMapHUD requires Player icon assigned in the scene.", this);
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

            // Minimap chỉ render mặt đất và những object/marker mà bạn chủ động
            // đặt vào MiniMapVisible. Cây, đá, model Player/Zombie vẫn ở layer thường.
            miniMapCamera.cullingMask = terrainMask | visibleMask;

            // Marker MiniMapVisible chỉ dành cho minimap, không xuất hiện ở camera gameplay.
            directionCamera.cullingMask &= ~visibleMask;
        }

        private void ConfigureMiniMapCamera()
        {
            miniMapCamera.orthographic = true;
            miniMapCamera.orthographicSize = worldRadius;
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

        private void UpdatePlayerIcon()
        {
            RectTransform playerRect = playerIcon.rectTransform;
            playerRect.anchoredPosition = Vector2.zero;
            playerRect.localRotation = Quaternion.identity;

            if (!playerIcon.gameObject.activeSelf)
            {
                playerIcon.gameObject.SetActive(true);
            }
        }

        private Vector3 GetHeadingForward()
        {
            Vector3 forward = useCameraDirection ? directionCamera.transform.forward : player.forward;
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

        private void OnValidate()
        {
            worldRadius = Mathf.Max(5f, worldRadius);
            cameraHeight = Mathf.Max(1f, cameraHeight);

            if (miniMapCamera != null)
            {
                ConfigureCameraLayers();
                ConfigureMiniMapCamera();
            }
        }
    }
}
