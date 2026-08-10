using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ZombieInfinite
{
    [DisallowMultipleComponent]
    public sealed class MiniMapHUD : MonoBehaviour
    {
        private const string TerrainLayerName = "MiniMapTerrain";
        private const string VisibleLayerName = "MiniMapVisible";
        private const string PlayerMarkerName = "MiniMap Player Icon";
        private const string LegacyZombieIconPrefix = "Zombie-Icon-";

        [Header("Scene References")]
        [Tooltip("Player được gán trực tiếp trong scene.")]
        [SerializeField] private Transform player;

        [Tooltip("Camera gameplay dùng để xác định hướng phía trên của minimap.")]
        [SerializeField] private Camera directionCamera;

        [Tooltip("Camera minimap đặt sẵn trong scene. Camera này chỉ render MiniMapTerrain + MiniMapVisible.")]
        [SerializeField] private Camera miniMapCamera;

        [Tooltip("Bật: hướng nhìn Camera gameplay luôn ở phía trên minimap. Tắt: hướng trước Player ở phía trên.")]
        [SerializeField] private bool useCameraDirection = true;

        [Header("Scene-authored UI")]
        [Tooltip("RectTransform vùng hiển thị map. Object này được chuyển sang RawImage ở Edit Mode để nhận RenderTexture.")]
        [SerializeField] private RectTransform mapContent;

        [Tooltip("Icon UI Player cũ. Chỉ dùng làm nguồn Sprite khi tự tạo world marker trong Edit Mode rồi sẽ bị tắt.")]
        [SerializeField] private Image playerIcon;

        [Header("Map Settings")]
        [SerializeField, Min(5f)] private float worldRadius = 30f;
        [SerializeField, Min(1f)] private float cameraHeight = 50f;
        [SerializeField, Range(128, 1024)] private int renderTextureResolution = 512;
        [SerializeField] private Color emptyMapColor = new(0.04f, 0.05f, 0.04f, 1f);

        private GameMenuController gameMenu;
        private RawImage mapDisplay;
        private RenderTexture mapRenderTexture;

#if UNITY_EDITOR
        private bool editorSetupScheduled;
#endif

        private void Awake()
        {
            if (!ValidateSceneReferences())
            {
                enabled = false;
                return;
            }

            gameMenu = player.GetComponent<GameMenuController>();
            mapDisplay = mapContent.GetComponent<RawImage>();

            if (mapDisplay == null)
            {
                Debug.LogError(
                    "MapContent cần RawImage. Hãy mở ZombieInfiniteDemo sau khi Unity compile và Save Scene một lần.",
                    this);
                enabled = false;
                return;
            }

            if (playerIcon != null)
            {
                playerIcon.enabled = false;
            }

            ConfigureCameraLayers();
            ConfigureMiniMapCamera();
            CreateRenderTexture();
        }

        private void LateUpdate()
        {
            bool visible = gameMenu == null || !gameMenu.GameplayBlocked;
            SetVisible(visible);
            if (!visible)
            {
                return;
            }

            UpdateCamera(GetHeadingForward());
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

            // Chỉ render Terrain và các marker/icon minimap.
            // Model Player/Zombie, cây, đá, súng, UI gameplay... đều bị cull.
            if (miniMapCamera != null)
            {
                miniMapCamera.cullingMask = terrainMask | visibleMask;
            }

            // MiniMapVisible là layer chỉ dành cho marker minimap.
            if (directionCamera != null)
            {
                directionCamera.cullingMask &= ~visibleMask;
            }
        }

        private void ConfigureMiniMapCamera()
        {
            if (miniMapCamera == null)
            {
                return;
            }

            miniMapCamera.orthographic = true;
            miniMapCamera.orthographicSize = worldRadius;
            miniMapCamera.rect = new Rect(0f, 0f, 1f, 1f);
            miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
            miniMapCamera.backgroundColor = emptyMapColor;
        }

        private void CreateRenderTexture()
        {
            ReleaseRenderTexture();

            int resolution = Mathf.Clamp(renderTextureResolution, 128, 1024);
            var descriptor = new RenderTextureDescriptor(
                resolution,
                resolution,
                RenderTextureFormat.ARGB32,
                16)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = true
            };

            mapRenderTexture = new RenderTexture(descriptor)
            {
                name = "MiniMap RenderTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            mapRenderTexture.Create();

            miniMapCamera.targetTexture = mapRenderTexture;
            mapDisplay.texture = mapRenderTexture;
            mapDisplay.color = Color.white;
            mapDisplay.raycastTarget = false;
        }

        private void ReleaseRenderTexture()
        {
            if (miniMapCamera != null && miniMapCamera.targetTexture == mapRenderTexture)
            {
                miniMapCamera.targetTexture = null;
            }

            if (mapDisplay != null && mapDisplay.texture == mapRenderTexture)
            {
                mapDisplay.texture = null;
            }

            if (mapRenderTexture == null)
            {
                return;
            }

            if (mapRenderTexture.IsCreated())
            {
                mapRenderTexture.Release();
            }

            Destroy(mapRenderTexture);
            mapRenderTexture = null;
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

        private void OnDestroy()
        {
            ReleaseRenderTexture();
        }

        private void OnValidate()
        {
            worldRadius = Mathf.Max(5f, worldRadius);
            cameraHeight = Mathf.Max(1f, cameraHeight);
            renderTextureResolution = Mathf.Clamp(renderTextureResolution, 128, 1024);
            ConfigureCameraLayers();
            ConfigureMiniMapCamera();

#if UNITY_EDITOR
            ScheduleEditorSetup();
#endif
        }

#if UNITY_EDITOR
        private void ScheduleEditorSetup()
        {
            if (Application.isPlaying || editorSetupScheduled || mapContent == null)
            {
                return;
            }

            editorSetupScheduled = true;
            EditorApplication.delayCall += ApplyEditorSetup;
        }

        private void ApplyEditorSetup()
        {
            editorSetupScheduled = false;

            if (this == null || Application.isPlaying || mapContent == null)
            {
                return;
            }

            bool changed = false;
            EnsureRawImage(ref changed);
            EnsurePlayerWorldMarker(ref changed);
            CleanupLegacyZombieIcons(ref changed);

            if (changed && gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }

        private void EnsureRawImage(ref bool changed)
        {
            RawImage rawImage = mapContent.GetComponent<RawImage>();
            if (rawImage == null)
            {
                rawImage = Undo.AddComponent<RawImage>(mapContent.gameObject);
                rawImage.raycastTarget = false;
                rawImage.color = Color.white;
                changed = true;
            }

            Image oldMapImage = mapContent.GetComponent<Image>();
            if (oldMapImage != null && oldMapImage.enabled)
            {
                Undo.RecordObject(oldMapImage, "Disable old minimap Image");
                oldMapImage.enabled = false;
                changed = true;
            }
        }

        private void EnsurePlayerWorldMarker(ref bool changed)
        {
            if (player == null)
            {
                return;
            }

            int visibleLayer = LayerMask.NameToLayer(VisibleLayerName);
            if (visibleLayer < 0)
            {
                return;
            }

            Transform marker = player.Find(PlayerMarkerName);
            if (marker == null)
            {
                if (playerIcon == null || playerIcon.sprite == null)
                {
                    Debug.LogWarning(
                        "Không thể tạo MiniMap Player Icon vì Player Icon UI cũ chưa có Sprite.",
                        this);
                    return;
                }

                var markerObject = new GameObject(PlayerMarkerName);
                Undo.RegisterCreatedObjectUndo(markerObject, "Create minimap player marker");
                marker = markerObject.transform;
                marker.SetParent(player, false);
                marker.localPosition = new Vector3(0f, 3f, 0f);
                marker.localRotation = Quaternion.Euler(90f, 0f, 0f);
                marker.localScale = Vector3.one * 0.12f;
                markerObject.layer = visibleLayer;

                SpriteRenderer renderer = Undo.AddComponent<SpriteRenderer>(markerObject);
                renderer.sprite = playerIcon.sprite;
                renderer.sortingOrder = 101;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                changed = true;
            }
            else if (marker.gameObject.layer != visibleLayer)
            {
                Undo.RecordObject(marker.gameObject, "Set minimap player marker layer");
                marker.gameObject.layer = visibleLayer;
                changed = true;
            }

            if (playerIcon != null && playerIcon.enabled)
            {
                Undo.RecordObject(playerIcon, "Disable old minimap player UI icon");
                playerIcon.enabled = false;
                changed = true;
            }
        }

        private void CleanupLegacyZombieIcons(ref bool changed)
        {
            for (int i = mapContent.childCount - 1; i >= 0; i--)
            {
                Transform child = mapContent.GetChild(i);
                if (!child.name.StartsWith(LegacyZombieIconPrefix))
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(child.gameObject);
                changed = true;
            }
        }
#endif
    }
}
