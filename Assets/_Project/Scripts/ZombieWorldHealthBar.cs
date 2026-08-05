using UnityEngine;
using UnityEngine.UI;

namespace ZombieInfinite
{
    [RequireComponent(typeof(ZombieChaseReturn))]
    [DisallowMultipleComponent]
    public sealed class ZombieWorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Vector3 worldOffset = new(0f, 2.2f, 0f);
        [SerializeField, Min(0.1f)] private float worldScale = 0.01f;
        [SerializeField, Min(1f)] private float maxVisibleDistance = 35f;
        [SerializeField, Min(0f)] private float fadeStartDistance = 8f;
        [SerializeField, Min(0.1f)] private float fadeSpeed = 8f;
        [SerializeField, Min(0f)] private float showAfterDamageDuration = 2.5f;

        private static readonly RaycastHit[] AimHits = new RaycastHit[32];
        private static ZombieChaseReturn aimedZombie;
        private static int lastAimCheckFrame = -1;

        private ZombieChaseReturn zombie;
        private CanvasGroup canvasGroup;
        private Slider healthSlider;
        private Text healthText;
        private float showUntil;

        private void Awake()
        {
            zombie = GetComponent<ZombieChaseReturn>();
            BuildWorldUI();
            RefreshHealth(zombie.CurrentHealth, zombie.MaxHealth);
        }

        private void OnEnable()
        {
            if (zombie != null)
            {
                zombie.HealthChanged += RefreshHealth;
            }
        }

        private void OnDisable()
        {
            if (zombie != null)
            {
                zombie.HealthChanged -= RefreshHealth;
            }
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null || zombie == null || zombie.IsDead || canvasGroup == null)
            {
                if (canvasGroup != null) canvasGroup.alpha = 0f;
                return;
            }

            Transform uiTransform = canvasGroup.transform;
            uiTransform.position = transform.position + worldOffset;
            uiTransform.rotation = Quaternion.LookRotation(
                uiTransform.position - camera.transform.position,
                camera.transform.up);

            float distance = Vector3.Distance(camera.transform.position, uiTransform.position);
            bool requested = GetAimedZombie(camera) == zombie || Time.time < showUntil;
            float distanceAlpha = 1f - Mathf.InverseLerp(
                Mathf.Min(fadeStartDistance, maxVisibleDistance),
                maxVisibleDistance,
                distance);
            float targetAlpha = requested && distance <= maxVisibleDistance
                ? distanceAlpha
                : 0f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha,
                fadeSpeed * Time.unscaledDeltaTime);
        }

        public void ShowTemporarily()
        {
            showUntil = Mathf.Max(showUntil, Time.time + showAfterDamageDuration);
            RefreshHealth(zombie.CurrentHealth, zombie.MaxHealth);
        }

        private void RefreshHealth(int current, int maximum)
        {
            if (healthSlider != null)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = Mathf.Max(1, maximum);
                healthSlider.value = Mathf.Clamp(current, 0, maximum);
            }

            if (healthText != null)
            {
                healthText.text = $"{Mathf.Max(0, current)} / {Mathf.Max(1, maximum)}";
            }
        }

        private void BuildWorldUI()
        {
            GameObject root = new("Zombie Health UI", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            root.transform.localPosition = worldOffset;
            root.transform.localScale = Vector3.one * worldScale;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(140f, 28f);
            canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GameObject sliderObject = new("Health Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(root.transform, false);
            healthSlider = sliderObject.GetComponent<Slider>();
            healthSlider.interactable = false;
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            Stretch(sliderRect, Vector2.zero, Vector2.zero);

            Image background = CreateImage("Background", sliderObject.transform,
                new Color(0.04f, 0.04f, 0.04f, 0.92f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.zero);

            GameObject fillArea = new("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            Stretch(fillAreaRect, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            Image fill = CreateImage("Fill", fillArea.transform,
                new Color(0.1f, 0.85f, 0.18f, 1f));
            Stretch(fill.rectTransform, Vector2.zero, Vector2.zero);
            healthSlider.fillRect = fill.rectTransform;
            healthSlider.targetGraphic = fill;
            healthSlider.direction = Slider.Direction.LeftToRight;

            GameObject textObject = new("Health Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(root.transform, false);
            healthText = textObject.GetComponent<Text>();
            healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            healthText.fontSize = 16;
            healthText.fontStyle = FontStyle.Bold;
            healthText.alignment = TextAnchor.MiddleCenter;
            healthText.color = Color.white;
            healthText.raycastTarget = false;
            healthText.horizontalOverflow = HorizontalWrapMode.Overflow;
            healthText.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(healthText.rectTransform, Vector2.zero, Vector2.zero);
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject instance = new(objectName, typeof(RectTransform), typeof(Image));
            instance.transform.SetParent(parent, false);
            Image image = instance.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static ZombieChaseReturn GetAimedZombie(Camera camera)
        {
            if (lastAimCheckFrame == Time.frameCount) return aimedZombie;

            lastAimCheckFrame = Time.frameCount;
            aimedZombie = null;
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            int count = Physics.RaycastNonAlloc(ray, AimHits, 1000f, ~0,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = AimHits[i].collider;
                if (hitCollider == null ||
                    hitCollider.GetComponentInParent<TopDownPlayerController>() != null ||
                    AimHits[i].distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = AimHits[i].distance;
                aimedZombie = hitCollider.GetComponentInParent<ZombieChaseReturn>();
            }

            return aimedZombie;
        }

        private void OnValidate()
        {
            worldScale = Mathf.Max(0.001f, worldScale);
            maxVisibleDistance = Mathf.Max(1f, maxVisibleDistance);
            fadeStartDistance = Mathf.Max(0f, fadeStartDistance);
            fadeSpeed = Mathf.Max(0.1f, fadeSpeed);
            showAfterDamageDuration = Mathf.Max(0f, showAfterDamageDuration);
        }
    }
}
