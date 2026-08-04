using UnityEngine;

namespace ZombieInfinite
{
    public sealed class SurvivalHUD : MonoBehaviour
    {
        private PlayerSurvivalStats stats;
        private WeaponAmmoInventory ammo;
        private GameMenuController gameMenu;
        private GUIStyle labelStyle;

        private void Awake()
        {
            stats = GetComponent<PlayerSurvivalStats>();
            ammo = GetComponent<WeaponAmmoInventory>();
            gameMenu = GetComponent<GameMenuController>();
        }

        private void OnGUI()
        {
            if (stats == null || ammo == null ||
                (gameMenu != null && gameMenu.GameplayBlocked))
            {
                return;
            }

            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            float x = 24f;
            float y = Screen.height - 154f;
            DrawBar(new Rect(x, y, 260f, 22f), stats.Health / stats.MaxHealth,
                new Color(0.75f, 0.08f, 0.08f), $"HP {stats.Health:0}/{stats.MaxHealth:0}");
            DrawBar(new Rect(x, y + 30f, 260f, 22f), stats.Stamina / stats.MaxStamina,
                new Color(0.12f, 0.65f, 0.95f), $"STAMINA {stats.Stamina:0}/{stats.MaxStamina:0}");

            GUI.Label(new Rect(x, y + 60f, 360f, 24f),
                $"AMMO {ammo.RoundsInMagazine}/{ammo.MagazineSize}  |  MAGS {ammo.ReserveMagazines}", labelStyle);
            GUI.Label(new Rect(x, y + 84f, 360f, 24f),
                $"MEDKITS {stats.MedkitCount}  |  F: USE  |  R: RELOAD", labelStyle);

            int totalSeconds = Mathf.FloorToInt(Time.timeSinceLevelLoad);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            GUI.Label(new Rect(Screen.width - 170f, 20f, 150f, 26f),
                $"TIME {minutes:00}:{seconds:00}", labelStyle);

            if (stats.IsUsingMedkit)
            {
                float progress = 1f - stats.MedkitUseRemaining / Mathf.Max(0.01f, stats.MedkitUseDuration);
                DrawBar(new Rect(x, y - 30f, 260f, 20f), progress,
                    new Color(0.1f, 0.9f, 0.2f), $"HEALING {stats.MedkitUseRemaining:0.0}s");
            }
            else if (ammo.IsReloading)
            {
                float progress = 1f - ammo.ReloadRemaining / Mathf.Max(0.01f, ammo.ReloadDuration);
                DrawBar(new Rect(x, y - 30f, 260f, 20f), progress,
                    new Color(0.95f, 0.75f, 0.1f), $"RELOADING {ammo.ReloadRemaining:0.0}s");
            }
        }

        private void DrawBar(Rect rect, float normalized, Color color, string label)
        {
            GUI.Box(rect, GUIContent.none);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f,
                (rect.width - 4f) * Mathf.Clamp01(normalized), rect.height - 4f),
                Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(rect, label, labelStyle);
        }
    }
}
