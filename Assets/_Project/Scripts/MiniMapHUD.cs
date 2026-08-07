using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieInfinite
{
    [DisallowMultipleComponent]
    public sealed class MiniMapHUD : MonoBehaviour
    {
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

        [Tooltip("Pool icon Zombie đặt sẵn trong scene. Không Instantiate icon runtime.")]
        [SerializeField] private Image[] zombieIcons;

        [Header("Map Settings")]
        [SerializeField, Min(5f)] private float worldRadius = 30f;
        [SerializeField, Min(1f)] private float cameraHeight = 50f;

        [Tooltip("Khoảng đệm để icon không chạm sát mép khung.")]
        [SerializeField, Min(0f)] private float edgePadding = 8f;

        [Tooltip("Ẩn quái ở ngoài bán kính thay vì ghim icon ở mép.")]
        [SerializeField] private bool hideOutsideRadius = true;

        [Tooltip("Bao lâu quét lại danh sách Zombie. Vị trí icon vẫn cập nhật mỗi frame.")]
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.5f;

        private readonly List<ZombieChaseReturn> zombies = new();
        private readonly Vector3[] viewportCorners = new Vector3[4];

        private GameMenuController gameMenu;
        private float nextRefreshTime;
        private bool warnedIconCapacity;

        private void Awake()
        {
            if (!ValidateSceneReferences())
            {
                enabled = false;
                return;
            }

            gameMenu = player.GetComponent<GameMenuController>();
            SetAllZombieIconsVisible(false);
            ConfigureMiniMapCamera();
        }

        private void OnEnable()
        {
            nextRefreshTime = 0f;
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

            if (Time.unscaledTime >= nextRefreshTime)
            {
                RefreshZombieList();
                nextRefreshTime = Time.unscaledTime + refreshInterval;
            }

            UpdateMarkers(headingForward);
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

            if (zombieIcons == null || zombieIcons.Length == 0)
            {
                Debug.LogError("MiniMapHUD requires scene-authored Zombie icons.", this);
                valid = false;
            }

            return valid;
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

        private void RefreshZombieList()
        {
            ZombieChaseReturn[] found = FindObjectsByType<ZombieChaseReturn>(
                FindObjectsSortMode.None);

            zombies.Clear();
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && !found[i].IsDead)
                {
                    zombies.Add(found[i]);
                }
            }

            if (!warnedIconCapacity && zombies.Count > zombieIcons.Length)
            {
                warnedIconCapacity = true;
                Debug.LogWarning(
                    $"MiniMapHUD has {zombieIcons.Length} authored Zombie icons but found {zombies.Count} Zombies. " +
                    "Add more Zombie icon objects to the scene if maximumAlive is increased.",
                    this);
            }
        }

        private void UpdateMarkers(Vector3 headingForward)
        {
            RectTransform playerRect = playerIcon.rectTransform;
            playerRect.anchoredPosition = Vector2.zero;
            playerRect.localRotation = Quaternion.identity;
            if (!playerIcon.gameObject.activeSelf)
            {
                playerIcon.gameObject.SetActive(true);
            }

            Vector3 headingRight = Vector3.Cross(Vector3.up, headingForward).normalized;
            Rect rect = mapContent.rect;
            float usableHalfWidth = Mathf.Max(0f, rect.width * 0.5f - edgePadding);
            float usableHalfHeight = Mathf.Max(0f, rect.height * 0.5f - edgePadding);
            float radiusSquared = worldRadius * worldRadius;

            for (int i = 0; i < zombieIcons.Length; i++)
            {
                Image icon = zombieIcons[i];
                if (icon == null)
                {
                    continue;
                }

                if (i >= zombies.Count)
                {
                    icon.gameObject.SetActive(false);
                    continue;
                }

                ZombieChaseReturn zombie = zombies[i];
                if (zombie == null || zombie.IsDead)
                {
                    icon.gameObject.SetActive(false);
                    continue;
                }

                Vector3 worldOffset = zombie.transform.position - player.position;
                worldOffset.y = 0f;

                bool outside = worldOffset.sqrMagnitude > radiusSquared;
                if (outside && hideOutsideRadius)
                {
                    icon.gameObject.SetActive(false);
                    continue;
                }

                Vector2 localOffset = new(
                    Vector3.Dot(worldOffset, headingRight),
                    Vector3.Dot(worldOffset, headingForward));

                if (outside)
                {
                    localOffset = localOffset.normalized * worldRadius;
                }

                icon.rectTransform.anchoredPosition = new Vector2(
                    localOffset.x / worldRadius * usableHalfWidth,
                    localOffset.y / worldRadius * usableHalfHeight);
                icon.rectTransform.localRotation = Quaternion.identity;
                icon.gameObject.SetActive(true);
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

        private void SetAllZombieIconsVisible(bool visible)
        {
            if (zombieIcons == null)
            {
                return;
            }

            for (int i = 0; i < zombieIcons.Length; i++)
            {
                if (zombieIcons[i] != null)
                {
                    zombieIcons[i].gameObject.SetActive(visible);
                }
            }
        }

        private void OnDisable()
        {
            SetAllZombieIconsVisible(false);
            if (miniMapCamera != null)
            {
                miniMapCamera.enabled = false;
            }
        }

        private void OnValidate()
        {
            worldRadius = Mathf.Max(5f, worldRadius);
            cameraHeight = Mathf.Max(1f, cameraHeight);
            edgePadding = Mathf.Max(0f, edgePadding);
            refreshInterval = Mathf.Max(0.05f, refreshInterval);
        }
    }
}
