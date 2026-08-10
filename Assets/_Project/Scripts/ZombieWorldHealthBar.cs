using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ZombieInfinite
{
    [RequireComponent(typeof(ZombieChaseReturn))]
    [DisallowMultipleComponent]
    public sealed class ZombieWorldHealthBar : MonoBehaviour
    {
        [Header("Prefab UI")]
        [Tooltip("Root của thanh máu trong prefab. Nên là Canvas World Space.")]
        [SerializeField] private RectTransform healthBarRoot;

        [Tooltip("CanvasGroup dùng để hiện/ẩn toàn bộ thanh máu.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("Slider hiển thị lượng máu hiện tại.")]
        [SerializeField] private Slider healthSlider;

        [Tooltip("Text hiển thị dạng máu hiện tại / máu tối đa.")]
        [SerializeField] private TMP_Text healthText;

        [Header("Display")]
        [Tooltip("Luôn hiện thanh máu khi zombie còn sống.")]
        [SerializeField] private bool alwaysVisible = true;

        [Tooltip("Khoảng cách tối đa còn nhìn thấy thanh máu.")]
        [SerializeField, Min(1f)] private float maxVisibleDistance = 35f;

        [Tooltip("Tốc độ hiện/ẩn thanh máu.")]
        [SerializeField, Min(0.1f)] private float fadeSpeed = 10f;

        [Tooltip("Thời gian tiếp tục hiện sau khi zombie nhận sát thương.")]
        [SerializeField, Min(0f)] private float showAfterDamageDuration = 2.5f;

        private ZombieChaseReturn zombie;
        private Camera targetCamera;
        private Canvas worldCanvas;
        private float showUntil;

        private void Awake()
        {
            zombie = GetComponent<ZombieChaseReturn>();
            FindMissingPrefabReferences();
            ResolveGameplayCamera();

            if (healthSlider != null)
            {
                healthSlider.interactable = false;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (worldCanvas != null)
            {
                worldCanvas.enabled = true;
                if (targetCamera != null)
                {
                    worldCanvas.worldCamera = targetCamera;
                }
            }
        }

        private void OnEnable()
        {
            if (zombie == null)
            {
                zombie = GetComponent<ZombieChaseReturn>();
            }

            zombie.HealthChanged += RefreshHealth;
            RefreshHealth(zombie.CurrentHealth, zombie.MaxHealth);
        }

        private void OnDisable()
        {
            if (zombie != null)
            {
                zombie.HealthChanged -= RefreshHealth;
            }
        }

        private void Start()
        {
            ResolveGameplayCamera();
            RefreshHealth(zombie.CurrentHealth, zombie.MaxHealth);
        }

        private void LateUpdate()
        {
            if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            {
                ResolveGameplayCamera();
            }

            bool canShow = targetCamera != null &&
                           zombie != null &&
                           !zombie.IsDead &&
                           Vector3.Distance(targetCamera.transform.position, transform.position)
                           <= maxVisibleDistance;

            bool requested = alwaysVisible || Time.time < showUntil;
            float targetAlpha = canShow && requested ? 1f : 0f;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(
                    canvasGroup.alpha,
                    targetAlpha,
                    fadeSpeed * Time.unscaledDeltaTime);
            }
            else if (healthBarRoot != null)
            {
                healthBarRoot.gameObject.SetActive(targetAlpha > 0f);
            }

            if (canShow && healthBarRoot != null)
            {
                Transform cameraTransform = targetCamera.transform;
                healthBarRoot.rotation = Quaternion.LookRotation(
                    healthBarRoot.position - cameraTransform.position,
                    cameraTransform.up);
            }
        }

        public void ShowTemporarily()
        {
            showUntil = Mathf.Max(showUntil, Time.time + showAfterDamageDuration);

            if (zombie != null)
            {
                RefreshHealth(zombie.CurrentHealth, zombie.MaxHealth);
            }
        }

        private void RefreshHealth(int current, int maximum)
        {
            int safeMaximum = Mathf.Max(1, maximum);
            int safeCurrent = Mathf.Clamp(current, 0, safeMaximum);

            if (healthSlider != null)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = safeMaximum;
                healthSlider.wholeNumbers = true;
                healthSlider.SetValueWithoutNotify(safeCurrent);
            }

            if (healthText != null)
            {
                healthText.text = $"{safeCurrent} / {safeMaximum}";
            }
        }

        private void ResolveGameplayCamera()
        {
            // Camera trong ZombieInfiniteDemo hiện đang Untagged nên Camera.main trả về null.
            // TopDownCameraFollow nằm trực tiếp trên Camera gameplay, vì vậy ưu tiên lấy Camera tại đó.
            TopDownCameraFollow cameraFollow = FindFirstObjectByType<TopDownCameraFollow>();
            if (cameraFollow != null)
            {
                Camera gameplayCamera = cameraFollow.GetComponent<Camera>();
                if (gameplayCamera != null)
                {
                    targetCamera = gameplayCamera;
                }
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (worldCanvas != null && targetCamera != null)
            {
                worldCanvas.worldCamera = targetCamera;
            }
        }

        private void FindMissingPrefabReferences()
        {
            if (worldCanvas == null)
            {
                worldCanvas = GetComponentInChildren<Canvas>(true);
            }

            if (healthBarRoot == null && worldCanvas != null)
            {
                healthBarRoot = worldCanvas.transform as RectTransform;
            }

            Transform searchRoot = healthBarRoot != null ? healthBarRoot : transform;

            if (canvasGroup == null)
            {
                canvasGroup = searchRoot.GetComponentInChildren<CanvasGroup>(true);
            }

            if (healthSlider == null)
            {
                healthSlider = searchRoot.GetComponentInChildren<Slider>(true);
            }

            if (healthText == null)
            {
                healthText = searchRoot.GetComponentInChildren<TMP_Text>(true);
            }

            if (healthSlider == null || healthText == null)
            {
                Debug.LogError(
                    $"[{nameof(ZombieWorldHealthBar)}] Prefab '{name}' cần có Slider và Text " +
                    "ở object con, hoặc kéo chúng vào các ô Prefab UI trong Inspector.",
                    this);
            }
        }

        private void OnValidate()
        {
            maxVisibleDistance = Mathf.Max(1f, maxVisibleDistance);
            fadeSpeed = Mathf.Max(0.1f, fadeSpeed);
            showAfterDamageDuration = Mathf.Max(0f, showAfterDamageDuration);

            if (!Application.isPlaying)
            {
                FindMissingPrefabReferences();
            }
        }
    }
}
