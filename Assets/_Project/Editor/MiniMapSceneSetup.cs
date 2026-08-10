#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZombieInfinite.Editor
{
    [InitializeOnLoad]
    internal static class MiniMapSceneSetup
    {
        private const string TargetSceneName = "ZombieInfiniteDemo";
        private const string TerrainLayerName = "MiniMapTerrain";
        private const string MarkerLayerName = "MiniMapVisible";
        private const string PlayerMarkerName = "MiniMap Player Icon";

        static MiniMapSceneSetup()
        {
            EditorApplication.delayCall += ApplyWhenReady;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.name == TargetSceneName)
            {
                EditorApplication.delayCall += ApplyWhenReady;
            }
        }

        private static void ApplyWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ApplyWhenReady;
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != TargetSceneName)
            {
                return;
            }

            int terrainLayer = LayerMask.NameToLayer(TerrainLayerName);
            int markerLayer = LayerMask.NameToLayer(MarkerLayerName);
            if (terrainLayer < 0 || markerLayer < 0)
            {
                Debug.LogError(
                    $"MiniMap setup requires layers '{TerrainLayerName}' and '{MarkerLayerName}'.");
                return;
            }

            MiniMapHUD hud = Object.FindFirstObjectByType<MiniMapHUD>(FindObjectsInactive.Include);
            if (hud == null)
            {
                return;
            }

            bool sceneChanged = false;
            SerializedObject hudSerialized = new SerializedObject(hud);
            Transform player = hudSerialized.FindProperty("player").objectReferenceValue as Transform;
            RectTransform mapContent = hudSerialized.FindProperty("mapContent").objectReferenceValue as RectTransform;
            Image playerIcon = hudSerialized.FindProperty("playerIcon").objectReferenceValue as Image;
            Camera miniMapCamera = hudSerialized.FindProperty("miniMapCamera").objectReferenceValue as Camera;
            Camera directionCamera = hudSerialized.FindProperty("directionCamera").objectReferenceValue as Camera;

            if (mapContent != null)
            {
                RawImage rawImage = mapContent.GetComponent<RawImage>();
                if (rawImage == null)
                {
                    rawImage = Undo.AddComponent<RawImage>(mapContent.gameObject);
                    rawImage.raycastTarget = false;
                    rawImage.color = Color.white;
                    sceneChanged = true;
                }

                Image oldImage = mapContent.GetComponent<Image>();
                if (oldImage != null && oldImage.enabled)
                {
                    Undo.RecordObject(oldImage, "Disable old minimap Image");
                    oldImage.enabled = false;
                    sceneChanged = true;
                }

                sceneChanged |= RemoveLegacyZombieUiIcons(mapContent);
            }

            if (player != null && playerIcon != null && playerIcon.sprite != null)
            {
                sceneChanged |= EnsurePlayerMarker(player, playerIcon.sprite, markerLayer);
                if (playerIcon.enabled)
                {
                    Undo.RecordObject(playerIcon, "Disable old minimap player UI icon");
                    playerIcon.enabled = false;
                    sceneChanged = true;
                }
            }

            if (miniMapCamera != null)
            {
                int desiredMask = (1 << terrainLayer) | (1 << markerLayer);
                if (miniMapCamera.cullingMask != desiredMask)
                {
                    Undo.RecordObject(miniMapCamera, "Set minimap camera layers");
                    miniMapCamera.cullingMask = desiredMask;
                    miniMapCamera.orthographic = true;
                    miniMapCamera.rect = new Rect(0f, 0f, 1f, 1f);
                    sceneChanged = true;
                }
            }

            if (directionCamera != null)
            {
                int desiredMask = directionCamera.cullingMask & ~(1 << markerLayer);
                if (directionCamera.cullingMask != desiredMask)
                {
                    Undo.RecordObject(directionCamera, "Hide minimap markers from gameplay camera");
                    directionCamera.cullingMask = desiredMask;
                    sceneChanged = true;
                }
            }

            sceneChanged |= ApplyTerrainLayersToSceneAndPrefabs(terrainLayer);
            sceneChanged |= EnsureZombieMarkers(markerLayer);

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("MiniMap setup applied: Terrain=MiniMapTerrain, Player/Enemy icons=MiniMapVisible, MapContent=RawImage.");
            }
        }

        private static bool EnsurePlayerMarker(Transform player, Sprite sprite, int markerLayer)
        {
            bool changed = false;
            Transform marker = player.Find(PlayerMarkerName);
            if (marker == null)
            {
                GameObject markerObject = new GameObject(PlayerMarkerName);
                Undo.RegisterCreatedObjectUndo(markerObject, "Create minimap player marker");
                marker = markerObject.transform;
                marker.SetParent(player, false);
                marker.localPosition = new Vector3(0f, 3f, 0f);
                marker.localRotation = Quaternion.Euler(90f, 0f, 0f);
                marker.localScale = Vector3.one * 0.12f;

                SpriteRenderer renderer = Undo.AddComponent<SpriteRenderer>(markerObject);
                renderer.sprite = sprite;
                renderer.sortingOrder = 101;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                changed = true;
            }

            if (marker.gameObject.layer != markerLayer)
            {
                Undo.RecordObject(marker.gameObject, "Set minimap player marker layer");
                marker.gameObject.layer = markerLayer;
                changed = true;
            }

            SpriteRenderer spriteRenderer = marker.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != sprite)
            {
                Undo.RecordObject(spriteRenderer, "Set minimap player sprite");
                spriteRenderer.sprite = sprite;
                changed = true;
            }

            return changed;
        }

        private static bool RemoveLegacyZombieUiIcons(RectTransform mapContent)
        {
            bool changed = false;
            for (int i = mapContent.childCount - 1; i >= 0; i--)
            {
                Transform child = mapContent.GetChild(i);
                if (!child.name.StartsWith("Zombie-Icon-"))
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(child.gameObject);
                changed = true;
            }

            return changed;
        }

        private static bool ApplyTerrainLayersToSceneAndPrefabs(int terrainLayer)
        {
            bool changed = false;

            ProceduralTerrainChunkManager[] managers =
                Object.FindObjectsByType<ProceduralTerrainChunkManager>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (ProceduralTerrainChunkManager manager in managers)
            {
                SerializedObject serialized = new SerializedObject(manager);
                SerializedProperty prefabs = serialized.FindProperty("chunkPrefabs");
                if (prefabs == null || !prefabs.isArray)
                {
                    continue;
                }

                for (int i = 0; i < prefabs.arraySize; i++)
                {
                    GameObject prefab = prefabs.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (prefab == null)
                    {
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(prefab);
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    bool prefabChanged = false;
                    Terrain[] terrains = root.GetComponentsInChildren<Terrain>(true);
                    foreach (Terrain terrain in terrains)
                    {
                        if (terrain.gameObject.layer != terrainLayer)
                        {
                            terrain.gameObject.layer = terrainLayer;
                            prefabChanged = true;
                        }
                    }

                    if (prefabChanged)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changed = true;
                    }

                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Terrain[] sceneTerrains = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Terrain terrain in sceneTerrains)
            {
                if (EditorUtility.IsPersistent(terrain) || terrain.gameObject.layer == terrainLayer)
                {
                    continue;
                }

                Undo.RecordObject(terrain.gameObject, "Set terrain minimap layer");
                terrain.gameObject.layer = terrainLayer;
                changed = true;
            }

            return changed;
        }

        private static bool EnsureZombieMarkers(int markerLayer)
        {
            bool changed = false;
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab Zombie", new[] { "Assets/_Project/Prefabs" });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool prefabChanged = false;

                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in transforms)
                {
                    if (child.name != "MiniMap Icon")
                    {
                        continue;
                    }

                    if (child.gameObject.layer != markerLayer)
                    {
                        child.gameObject.layer = markerLayer;
                        prefabChanged = true;
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed = true;
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            return changed;
        }
    }
}
#endif
