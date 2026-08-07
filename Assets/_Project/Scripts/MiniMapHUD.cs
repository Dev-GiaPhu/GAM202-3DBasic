using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieInfinite
{
    [DisallowMultipleComponent]
    public sealed class MiniMapHUD : MonoBehaviour
    {
        [Header("World References")]
        [Tooltip("Player được đặt ở tâm mini map. Để trống, script sẽ tự tìm TopDownPlayerController.")]
        [SerializeField] private Transform player;

        [Tooltip("Camera quyết định hướng phía trên của mini map. Nên gán Main Camera.")]
        [SerializeField] private Camera directionCamera;

        [Tooltip("Bật: hướng nhìn Camera luôn ở phía trên. Tắt: hướng trước của Player ở phía trên.")]
        [SerializeField] private bool useCameraDirection = true;

        [Header("Editable UI")]
        [Tooltip("Vùng RectTransform chứa các icon. Có thể gắn RectMask2D để cắt icon ngoài khung.")]
        [SerializeField] private RectTransform mapContent;

        [Tooltip("Image icon Player đặt sẵn ở giữa map.")]
        [SerializeField] private Image playerIcon;

        [Tooltip("Image mẫu của Zombie. Nên để object mẫu inactive trong Hierarchy.")]
        [SerializeField] private Image zombieIconTemplate;

        [Header("Map Settings")]
        [SerializeField, Min(5f)] private float worldRadius = 30f;

        [Tooltip("Khoảng đệm để icon không chạm sát mép khung.")]
        [SerializeField, Min(0f)] private float edgePadding = 8f;

        [Tooltip("Ẩn quái ở ngoài bán kính thay vì ghim icon ở mép.")]
        [SerializeField] private bool hideOutsideRadius = true;

        [Tooltip("Bao lâu quét lại danh sách Zombie. Vị trí icon vẫn cập nhật mỗi frame.")]
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.5f;

        private readonly List<ZombieChaseReturn> zombies = new();
        private readonly List<Image> zombieIcons = new();

        private GameMenuController gameMenu;
        private float nextRefreshTime;

        private void Awake()
        {
            ResolveReferences();

            if (zombieIconTemplate != null &&
                zombieIconTemplate.gameObject.scene.IsValid())
            {
                zombieIconTemplate.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            nextRefreshTime = 0f;
        }

        private void LateUpdate()
        {
            ResolveReferences();

            bool visible = gameMenu == null || !gameMenu.GameplayBlocked;
            if (mapContent != null && mapContent.gameObject.activeSelf != visible)
            {
                mapContent.gameObject.SetActive(visible);
            }

            if (!visible || player == null || mapContent == null)
            {
                return;
            }

            if (Time.unscaledTime >= nextRefreshTime)
            {
                RefreshZombieList();
                nextRefreshTime = Time.unscaledTime + refreshInterval;
            }

            UpdateMarkers();
        }

        private void ResolveReferences()
        {
            if (player == null)
            {
                TopDownPlayerController controller =
                    FindFirstObjectByType<TopDownPlayerController>();

                if (controller != null)
                {
                    player = controller.transform;
                }
            }

            if (directionCamera == null)
            {
                directionCamera = Camera.main;
            }

            if (player != null && gameMenu == null)
            {
                gameMenu = player.GetComponent<GameMenuController>();
            }
        }

        private void RefreshZombieList()
        {
            ZombieChaseReturn[] found = FindObjectsByType<ZombieChaseReturn>(
                FindObjectsSortMode.None);

            zombies.Clear();
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    zombies.Add(found[i]);
                }
            }

            EnsureIconCount(zombies.Count);
        }

        private void EnsureIconCount(int requiredCount)
        {
            if (zombieIconTemplate == null || mapContent == null)
            {
                return;
            }

            while (zombieIcons.Count < requiredCount)
            {
                Image icon = Instantiate(zombieIconTemplate, mapContent);
                icon.name = "Zombie Icon " + (zombieIcons.Count + 1);
                icon.gameObject.SetActive(false);
                zombieIcons.Add(icon);
            }

            for (int i = requiredCount; i < zombieIcons.Count; i++)
            {
                zombieIcons[i].gameObject.SetActive(false);
            }
        }

        private void UpdateMarkers()
        {
            if (playerIcon != null)
            {
                RectTransform playerRect = playerIcon.rectTransform;
                playerRect.anchoredPosition = Vector2.zero;
                playerRect.localRotation = Quaternion.identity;
                playerIcon.gameObject.SetActive(true);
            }

            Vector3 headingForward = GetHeadingForward();
            Vector3 headingRight = Vector3.Cross(Vector3.up, headingForward).normalized;

            Rect rect = mapContent.rect;
            float usableHalfWidth = Mathf.Max(0f, rect.width * 0.5f - edgePadding);
            float usableHalfHeight = Mathf.Max(0f, rect.height * 0.5f - edgePadding);
            float radiusSquared = worldRadius * worldRadius;

            for (int i = 0; i < zombieIcons.Count; i++)
            {
                if (i >= zombies.Count)
                {
                    zombieIcons[i].gameObject.SetActive(false);
                    continue;
                }

                ZombieChaseReturn zombie = zombies[i];
                Image icon = zombieIcons[i];

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

                Vector2 uiPosition = new(
                    localOffset.x / worldRadius * usableHalfWidth,
                    localOffset.y / worldRadius * usableHalfHeight);

                icon.rectTransform.anchoredPosition = uiPosition;
                icon.rectTransform.localRotation = Quaternion.identity;
                icon.gameObject.SetActive(true);
            }
        }

        private Vector3 GetHeadingForward()
        {
            Vector3 forward = player != null ? player.forward : Vector3.forward;

            if (useCameraDirection && directionCamera != null)
            {
                forward = directionCamera.transform.forward;
            }

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            return forward.normalized;
        }

        private void OnDisable()
        {
            for (int i = 0; i < zombieIcons.Count; i++)
            {
                if (zombieIcons[i] != null)
                {
                    zombieIcons[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnValidate()
        {
            worldRadius = Mathf.Max(5f, worldRadius);
            edgePadding = Mathf.Max(0f, edgePadding);
            refreshInterval = Mathf.Max(0.05f, refreshInterval);
        }
    }
}
