using UnityEngine;

namespace ZombieInfinite
{
    [RequireComponent(typeof(ZombieChaseReturn))]
    public sealed class ZombieWorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Vector3 worldOffset = new(0f, 2.2f, 0f);
        [SerializeField, Min(20f)] private float width = 70f;
        [SerializeField, Min(3f)] private float height = 9f;
        [SerializeField, Min(1f)] private float maxVisibleDistance = 35f;
        [SerializeField, Min(0f)] private float fadeStartDistance = 8f;
        [SerializeField, Min(0.1f)] private float aimFadeSpeed = 8f;
        [SerializeField, Min(0f)] private float showAfterDamageDuration = 2.5f;

        private static readonly RaycastHit[] AimHits = new RaycastHit[32];
        private static ZombieChaseReturn aimedZombie;
        private static int lastAimCheckFrame = -1;

        private ZombieChaseReturn zombie;
        private float visibility;
        private float showUntil;

        private void Awake()
        {
            zombie = GetComponent<ZombieChaseReturn>();
        }

        private void OnGUI()
        {
            Camera camera = Camera.main;
            if (camera == null || zombie == null || zombie.IsDead)
            {
                return;
            }

            ZombieChaseReturn currentAim = GetAimedZombie(camera);
            float targetVisibility = currentAim == zombie || Time.time < showUntil ? 1f : 0f;
            visibility = Mathf.MoveTowards(
                visibility,
                targetVisibility,
                aimFadeSpeed * Time.unscaledDeltaTime);

            Vector3 worldPosition = transform.position + worldOffset;
            float distance = Vector3.Distance(camera.transform.position, worldPosition);
            float distanceAlpha = 1f - Mathf.InverseLerp(
                Mathf.Min(fadeStartDistance, maxVisibleDistance),
                maxVisibleDistance,
                distance);
            float alpha = visibility * distanceAlpha;
            if (alpha <= 0.001f || distance > maxVisibleDistance)
            {
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f)
            {
                return;
            }

            var rect = new Rect(
                screen.x - width * 0.5f,
                Screen.height - screen.y,
                width,
                height);

            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.82f * alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.15f, 0.9f, 0.15f, alpha);
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f,
                (rect.width - 2f) * zombie.HealthNormalized, rect.height - 2f),
                Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        public void ShowTemporarily()
        {
            showUntil = Mathf.Max(showUntil, Time.time + showAfterDamageDuration);
        }

        private static ZombieChaseReturn GetAimedZombie(Camera camera)
        {
            if (lastAimCheckFrame == Time.frameCount)
            {
                return aimedZombie;
            }

            lastAimCheckFrame = Time.frameCount;
            aimedZombie = null;
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            int count = Physics.RaycastNonAlloc(
                ray,
                AimHits,
                1000f,
                ~0,
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
    }
}
