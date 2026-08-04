using UnityEngine;

namespace ZombieInfinite
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSurvivalStats))]
    public sealed class PlayerDamageFeedback : MonoBehaviour
    {
        [Header("Screen Damage")]
        [Tooltip("Optional blood image. Leave empty to use a solid red flash.")]
        [SerializeField] private Texture2D bloodOverlay;
        [SerializeField] private Color flashColor = new(0.8f, 0f, 0f, 0.65f);
        [SerializeField, Min(0.05f)] private float flashDuration = 0.45f;

        [Header("Camera Shake")]
        [SerializeField, Min(0f)] private float shakeStrength = 1f;

        private PlayerSurvivalStats stats;
        private TopDownCameraFollow cameraFollow;
        private float flashRemaining;

        private void Awake()
        {
            stats = GetComponent<PlayerSurvivalStats>();
            cameraFollow = FindFirstObjectByType<TopDownCameraFollow>();
        }

        private void OnEnable()
        {
            if (stats != null)
            {
                stats.Damaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (stats != null)
            {
                stats.Damaged -= HandleDamaged;
            }
        }

        private void Update()
        {
            flashRemaining = Mathf.Max(0f, flashRemaining - Time.unscaledDeltaTime);
        }

        private void HandleDamaged(float damage, float maxHealth)
        {
            flashRemaining = flashDuration;
            if (cameraFollow == null)
            {
                cameraFollow = FindFirstObjectByType<TopDownCameraFollow>();
            }

            float normalizedDamage = maxHealth > 0f ? damage / maxHealth : 0f;
            cameraFollow?.TriggerDamageShake(shakeStrength * Mathf.Lerp(0.65f, 1.5f, normalizedDamage));
        }

        private void OnGUI()
        {
            if (flashRemaining <= 0f)
            {
                return;
            }

            float normalized = Mathf.Clamp01(flashRemaining / flashDuration);
            Color oldColor = GUI.color;
            GUI.color = new Color(flashColor.r, flashColor.g, flashColor.b,
                flashColor.a * normalized);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                bloodOverlay != null ? bloodOverlay : Texture2D.whiteTexture,
                ScaleMode.StretchToFill);
            GUI.color = oldColor;
        }
    }
}
