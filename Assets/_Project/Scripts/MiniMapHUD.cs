using UnityEngine;

namespace ZombieInfinite
{
    [DisallowMultipleComponent]
    public sealed class MiniMapHUD : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField, Min(80f)] private float size = 180f;
        [SerializeField, Min(0f)] private float margin = 20f;
        [SerializeField, Min(5f)] private float worldRadius = 30f;

        [Header("Markers")]
        [SerializeField, Min(2f)] private float playerMarkerSize = 10f;
        [SerializeField, Min(2f)] private float zombieMarkerSize = 7f;
        [SerializeField] private Color backgroundColor = new(0.03f, 0.05f, 0.05f, 0.78f);
        [SerializeField] private Color borderColor = new(0.75f, 0.85f, 0.85f, 0.9f);
        [SerializeField] private Color playerColor = new(0.15f, 0.85f, 1f, 1f);
        [SerializeField] private Color zombieColor = new(0.95f, 0.12f, 0.08f, 1f);

        private Texture2D whiteTexture;
        private GUIStyle titleStyle;
        private GameMenuController gameMenu;

        private void Awake()
        {
            whiteTexture = Texture2D.whiteTexture;
            gameMenu = GetComponent<GameMenuController>();
        }

        private void OnGUI()
        {
            if (gameMenu != null && gameMenu.GameplayBlocked)
            {
                return;
            }

            Rect mapRect = new(Screen.width - size - margin, margin, size, size);
            DrawRect(mapRect, backgroundColor);
            DrawBorder(mapRect, 2f, borderColor);

            Vector2 center = mapRect.center;
            float mapRadius = size * 0.5f - 8f;
            ZombieChaseReturn[] zombies = FindObjectsByType<ZombieChaseReturn>(
                FindObjectsSortMode.None);

            for (int i = 0; i < zombies.Length; i++)
            {
                ZombieChaseReturn zombie = zombies[i];
                if (zombie == null || zombie.IsDead)
                {
                    continue;
                }

                Vector3 offset = zombie.transform.position - transform.position;
                Vector2 planar = new(offset.x, offset.z);
                if (planar.sqrMagnitude > worldRadius * worldRadius)
                {
                    continue;
                }

                // Mini map quay theo hướng nhìn của Player: phía trước luôn ở trên.
                float angle = -transform.eulerAngles.y * Mathf.Deg2Rad;
                Vector2 rotated = new(
                    planar.x * Mathf.Cos(angle) - planar.y * Mathf.Sin(angle),
                    planar.x * Mathf.Sin(angle) + planar.y * Mathf.Cos(angle));
                Vector2 marker = center + new Vector2(rotated.x, -rotated.y) /
                    worldRadius * mapRadius;
                DrawMarker(marker, zombieMarkerSize, zombieColor);
            }

            DrawMarker(center, playerMarkerSize, playerColor);

            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(mapRect.x, mapRect.yMax + 2f, mapRect.width, 20f),
                $"MINI MAP  {worldRadius:0}m", titleStyle);
        }

        private void DrawMarker(Vector2 center, float markerSize, Color color)
        {
            DrawRect(new Rect(center.x - markerSize * 0.5f,
                center.y - markerSize * 0.5f, markerSize, markerSize), color);
        }

        private void DrawBorder(Rect rect, float thickness, Color color)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = previous;
        }

        private void OnValidate()
        {
            size = Mathf.Max(80f, size);
            margin = Mathf.Max(0f, margin);
            worldRadius = Mathf.Max(5f, worldRadius);
            playerMarkerSize = Mathf.Max(2f, playerMarkerSize);
            zombieMarkerSize = Mathf.Max(2f, zombieMarkerSize);
        }
    }
}
