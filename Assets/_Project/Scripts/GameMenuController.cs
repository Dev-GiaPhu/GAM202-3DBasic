using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZombieInfinite
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSurvivalStats))]
    public sealed class GameMenuController : MonoBehaviour
    {
        private enum MenuState
        {
            Playing,
            MainMenu,
            Paused,
            GameOver
        }

        [Header("Startup")]
        [SerializeField] private bool showMainMenuOnStart = true;

        [Header("Text")]
        [SerializeField] private string gameTitle = "ZOMBIE SURVIVAL";

        private static bool restartImmediately;

        private PlayerSurvivalStats stats;
        private MenuState state;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle buttonStyle;
        private Texture2D overlayTexture;

        public bool GameplayBlocked => state != MenuState.Playing;

        private void Awake()
        {
            stats = GetComponent<PlayerSurvivalStats>();
            if (restartImmediately)
            {
                restartImmediately = false;
                SetState(MenuState.Playing);
            }
            else
            {
                SetState(showMainMenuOnStart ? MenuState.MainMenu : MenuState.Playing);
            }
        }

        private void Update()
        {
            // Menu is the cursor-state owner while gameplay is blocked. This
            // also protects the buttons from another camera/input script that
            // attempts to lock the cursor during initialization.
            if (GameplayBlocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (state != MenuState.GameOver && stats != null && stats.Health <= 0f)
            {
                SetState(MenuState.GameOver);
                return;
            }

            if (Keyboard.current == null ||
                !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (state == MenuState.Playing)
            {
                SetState(MenuState.Paused);
            }
            else if (state == MenuState.Paused)
            {
                SetState(MenuState.Playing);
            }
        }

        private void OnGUI()
        {
            if (state == MenuState.Playing)
            {
                return;
            }

            EnsureStyles();
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), overlayTexture);

            float panelWidth = Mathf.Min(460f, Screen.width - 40f);
            float panelHeight = state == MenuState.Paused ? 410f : 360f;
            var panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 35f, panel.y + 25f,
                panel.width - 70f, panel.height - 50f));

            if (state == MenuState.MainMenu)
            {
                GUILayout.Label(gameTitle, titleStyle);
                GUILayout.Label("SURVIVE THE ENDLESS HORDE", subtitleStyle);
                GUILayout.Space(42f);
                if (GUILayout.Button("START GAME", buttonStyle)) SetState(MenuState.Playing);
                GUILayout.Space(12f);
                if (GUILayout.Button("QUIT", buttonStyle)) QuitGame();
            }
            else if (state == MenuState.Paused)
            {
                GUILayout.Label("PAUSED", titleStyle);
                GUILayout.Space(25f);
                if (GUILayout.Button("RESUME", buttonStyle)) SetState(MenuState.Playing);
                GUILayout.Space(8f);
                if (GUILayout.Button("RESTART", buttonStyle)) RestartGame();
                GUILayout.Space(8f);
                if (GUILayout.Button("MAIN MENU", buttonStyle)) ReloadToMainMenu();
                GUILayout.Space(8f);
                if (GUILayout.Button("QUIT", buttonStyle)) QuitGame();
            }
            else
            {
                GUILayout.Label("YOU DIED", titleStyle);
                GUILayout.Label($"SURVIVED  {FormatTime(Time.timeSinceLevelLoad)}", subtitleStyle);
                GUILayout.Space(34f);
                if (GUILayout.Button("TRY AGAIN", buttonStyle)) RestartGame();
                GUILayout.Space(12f);
                if (GUILayout.Button("MAIN MENU", buttonStyle)) ReloadToMainMenu();
            }

            GUILayout.EndArea();
        }

        private void SetState(MenuState newState)
        {
            state = newState;
            bool playing = state == MenuState.Playing;
            Time.timeScale = playing ? 1f : 0f;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
        }

        private void RestartGame()
        {
            restartImmediately = true;
            ReloadScene();
        }

        private void ReloadToMainMenu()
        {
            restartImmediately = false;
            ReloadScene();
        }

        private static void ReloadScene()
        {
            Time.timeScale = 1f;
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }

        private static void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            Debug.Log("Quit only closes the application in a built game.");
#endif
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.85f, 0.12f, 0.08f) }
            };
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                normal = { textColor = Color.white }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 48f,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            overlayTexture = new Texture2D(1, 1);
            overlayTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            overlayTexture.Apply();
        }

        private static string FormatTime(float time)
        {
            int seconds = Mathf.Max(0, Mathf.FloorToInt(time));
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        private void OnDestroy()
        {
            if (overlayTexture != null)
            {
                Destroy(overlayTexture);
            }
        }
    }
}
