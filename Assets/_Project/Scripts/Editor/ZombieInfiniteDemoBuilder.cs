using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using ZombieInfinite;

public static class ZombieInfiniteDemoBuilder
{
    private const string ScenePath = "Assets/_Project/Scenes/ZombieInfiniteDemo.unity";
    private const string PrefabFolder = "Assets/_Project/Prefabs";
    private const string MaterialFolder = "Assets/_Project/Art/Materials";
    private const string ChunkPrefabFolder = "Assets/_Project/Prefabs/TerrainChunks";
    private const string ChunkDataFolder = "Assets/_Project/Terrain/ChunkData";
    private const string TerrainLayerFolder = "Assets/_Project/Terrain/Layers";

    [MenuItem("Tools/Zombie Infinite/Build Demo Scene")]
    public static void Build()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);
        EnsureFolder(ChunkPrefabFolder);
        EnsureFolder(ChunkDataFolder);
        EnsureFolder(TerrainLayerFolder);

        GameObject[] chunkPrefabs = CreateChunkPrefabs();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "ZombieInfiniteDemo";

        Material playerMaterial = CreateMaterial("Player_Blue", new Color(0.1f, 0.45f, 1f));
        Material gunMaterial = CreateMaterial("Gun_Dark", new Color(0.08f, 0.08f, 0.1f));
        Material zombieMaterial = CreateMaterial("Zombie_Green", new Color(0.18f, 0.55f, 0.15f));
        Material obstacleMaterial = CreateMaterial("Obstacle_Orange", new Color(1f, 0.35f, 0.05f));
        Material linkMaterial = CreateMaterial("Link_Blue", new Color(0.1f, 0.65f, 1f));
        Material modifierMaterial = CreateMaterial("Modifier_Purple", new Color(0.55f, 0.15f, 0.8f, 0.45f));

        GameObject lighting = new("Lighting");
        Light sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.transform.SetParent(lighting.transform);
        sun.type = LightType.Directional;
        sun.intensity = 1.25f;
        sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        GameObject player = BuildPlayer(playerMaterial, gunMaterial);
        GameObject playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, $"{PrefabFolder}/Player.prefab");
        Object.DestroyImmediate(player);
        player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.name = "Player";
        player.transform.position = new Vector3(20f, 5f, 20f);

        Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.gameObject.AddComponent<AudioListener>();
        TopDownCameraFollow cameraFollow = camera.gameObject.AddComponent<TopDownCameraFollow>();
        cameraFollow.Configure(player.transform);
        camera.transform.position = player.transform.position + new Vector3(0f, 18f, -10.4f);
        camera.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

        GameObject world = new("Infinite Terrain System");
        NavMeshSurface surface = world.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        ProceduralTerrainChunkManager chunks = world.AddComponent<ProceduralTerrainChunkManager>();
        chunks.Configure(player.transform, surface, chunkPrefabs);

        GameObject zombie = BuildZombie(zombieMaterial);
        GameObject zombiePrefab = PrefabUtility.SaveAsPrefabAsset(zombie, $"{PrefabFolder}/Zombie.prefab");
        Object.DestroyImmediate(zombie);
        zombie = (GameObject)PrefabUtility.InstantiatePrefab(zombiePrefab, scene);
        zombie.name = "Zombie - 5m Chase Demo";
        zombie.transform.position = new Vector3(27f, 5f, 20f);
        zombie.GetComponent<ZombieChaseReturn>().Configure(player.transform);

        BuildNavigationDemos(obstacleMaterial, linkMaterial, modifierMaterial);
        BuildInstructions();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        Debug.Log($"Built zombie infinite terrain demo at {ScenePath}");
    }

    private static GameObject BuildPlayer(Material bodyMaterial, Material gunMaterial)
    {
        GameObject root = new("Player");
        CharacterController controller = root.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.45f;
        controller.center = new Vector3(0f, 1f, 0f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = Vector3.up;
        body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        GameObject gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gun.name = "Gun";
        gun.transform.SetParent(root.transform, false);
        gun.transform.localPosition = new Vector3(0.45f, 1.2f, 0.65f);
        gun.transform.localScale = new Vector3(0.18f, 0.18f, 1.2f);
        gun.GetComponent<Renderer>().sharedMaterial = gunMaterial;
        Object.DestroyImmediate(gun.GetComponent<Collider>());

        GameObject muzzle = new("Muzzle");
        muzzle.transform.SetParent(gun.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0f, 0.55f);
        root.AddComponent<TopDownPlayerController>().Configure(muzzle.transform);
        return root;
    }

    private static GameObject BuildZombie(Material material)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        root.name = "Zombie";
        root.GetComponent<Renderer>().sharedMaterial = material;
        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        agent.speed = 3.5f;
        agent.angularSpeed = 720f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 0.9f;
        root.AddComponent<ZombieChaseReturn>();
        return root;
    }

    private static void BuildNavigationDemos(Material obstacleMaterial, Material linkMaterial, Material modifierMaterial)
    {
        GameObject demos = new("AI Navigation Assignment Demos");

        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obstacle.name = "Bai 02 - Carving NavMesh Obstacle";
        obstacle.transform.SetParent(demos.transform);
        obstacle.transform.position = new Vector3(13f, 2f, 13f);
        obstacle.transform.localScale = new Vector3(1.3f, 2f, 1.3f);
        obstacle.GetComponent<Renderer>().sharedMaterial = obstacleMaterial;
        NavMeshObstacle navObstacle = obstacle.AddComponent<NavMeshObstacle>();
        navObstacle.shape = NavMeshObstacleShape.Capsule;
        navObstacle.carving = true;

        GameObject platformA = CreatePlatform("Bai 03 - Link Platform A", new Vector3(31f, 2.5f, 11f), linkMaterial);
        GameObject platformB = CreatePlatform("Bai 03 - Link Platform B", new Vector3(31f, 2.5f, 18f), linkMaterial);
        platformA.transform.SetParent(demos.transform);
        platformB.transform.SetParent(demos.transform);
        NavMeshLink link = platformA.AddComponent<NavMeshLink>();
        link.startPoint = new Vector3(0f, 0.55f, 2f);
        link.endPoint = new Vector3(0f, 0.55f, 5f);
        link.width = 2f;
        link.bidirectional = true;

        GameObject modifier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        modifier.name = "Bai 04 - Mud Modifier Volume";
        modifier.transform.SetParent(demos.transform);
        modifier.transform.position = new Vector3(20f, 1.5f, 30f);
        modifier.transform.localScale = new Vector3(8f, 3f, 5f);
        modifier.GetComponent<Renderer>().sharedMaterial = modifierMaterial;
        Object.DestroyImmediate(modifier.GetComponent<Collider>());
        NavMeshModifierVolume volume = modifier.AddComponent<NavMeshModifierVolume>();
        volume.size = new Vector3(8f, 3f, 5f);
        volume.center = Vector3.zero;
        volume.area = 3;
    }

    private static GameObject CreatePlatform(string objectName, Vector3 position, Material material)
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = objectName;
        platform.transform.position = position;
        platform.transform.localScale = new Vector3(5f, 1f, 4f);
        platform.GetComponent<Renderer>().sharedMaterial = material;
        return platform;
    }

    private static void BuildInstructions()
    {
        GameObject instructions = new("README - Controls and Assignments");
        instructions.AddComponent<DemoInstructions>();
    }

    private static GameObject[] CreateChunkPrefabs()
    {
        TerrainLayer[] layers =
        {
            CreateTerrainLayerAsset("Grass", new Color(0.16f, 0.42f, 0.12f), 7f),
            CreateTerrainLayerAsset("Dirt", new Color(0.36f, 0.22f, 0.11f), 6f),
            CreateTerrainLayerAsset("Rock", new Color(0.36f, 0.37f, 0.39f), 5f)
        };

        var prefabs = new GameObject[4];
        for (int index = 0; index < prefabs.Length; index++)
        {
            char suffix = (char)('A' + index);
            string dataPath = $"{ChunkDataFolder}/TerrainChunk_{suffix}.asset";
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (data == null)
            {
                data = CreateInitialChunkData(index, layers);
                AssetDatabase.CreateAsset(data, dataPath);
            }

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = $"TerrainChunk_{suffix}";
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.allowAutoConnect = false;
            terrain.groupingID = 1;

            string prefabPath = $"{ChunkPrefabFolder}/TerrainChunk_{suffix}.prefab";
            prefabs[index] = PrefabUtility.SaveAsPrefabAsset(terrainObject, prefabPath);
            Object.DestroyImmediate(terrainObject);
        }

        return prefabs;
    }

    private static TerrainData CreateInitialChunkData(int variant, TerrainLayer[] layers)
    {
        const int heightResolution = 65;
        const int alphaResolution = 64;
        var data = new TerrainData
        {
            heightmapResolution = heightResolution,
            alphamapResolution = alphaResolution,
            baseMapResolution = 128,
            size = new Vector3(40f, 8f, 40f),
            terrainLayers = layers
        };

        var heights = new float[heightResolution, heightResolution];
        for (int z = 0; z < heightResolution; z++)
        {
            for (int x = 0; x < heightResolution; x++)
            {
                float u = x / (heightResolution - 1f);
                float v = z / (heightResolution - 1f);
                float borderBlend = Mathf.Pow(Mathf.Sin(Mathf.PI * u) * Mathf.Sin(Mathf.PI * v), 2f);
                float hills = Mathf.PerlinNoise(u * (2.4f + variant * 0.25f) + variant * 3.7f,
                    v * (2.4f + variant * 0.2f) - variant * 2.9f);
                float interiorHeight = 0.17f + hills * (0.12f + variant * 0.012f);
                heights[z, x] = Mathf.Lerp(0.18f, interiorHeight, borderBlend);
            }
        }
        data.SetHeights(0, 0, heights);

        var splat = new float[alphaResolution, alphaResolution, layers.Length];
        for (int z = 0; z < alphaResolution; z++)
        {
            for (int x = 0; x < alphaResolution; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.065f + variant * 4.3f, z * 0.065f - variant * 3.1f);
                float rock = Mathf.SmoothStep(0.67f, 0.92f, noise);
                float dirt = Mathf.Clamp01((1f - Mathf.Abs(noise - 0.48f) * 5f) * 0.55f);
                float grass = Mathf.Max(0.05f, 1f - rock - dirt);
                float total = grass + dirt + rock;
                splat[z, x, 0] = grass / total;
                splat[z, x, 1] = dirt / total;
                splat[z, x, 2] = rock / total;
            }
        }
        data.SetAlphamaps(0, 0, splat);
        return data;
    }

    private static TerrainLayer CreateTerrainLayerAsset(string layerName, Color color, float tileSize)
    {
        string texturePath = $"{TerrainLayerFolder}/{layerName}_Texture.asset";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            texture = new Texture2D(4, 4, TextureFormat.RGB24, true)
            {
                name = $"{layerName}_Texture",
                wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                float variation = 0.9f + ((i * 37) % 5) * 0.035f;
                pixels[i] = color * variation;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, texturePath);
        }

        string layerPath = $"{TerrainLayerFolder}/{layerName}.terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (layer == null)
        {
            layer = new TerrainLayer
            {
                name = layerName,
                diffuseTexture = texture,
                tileSize = new Vector2(tileSize, tileSize)
            };
            AssetDatabase.CreateAsset(layer, layerPath);
        }

        return layer;
    }

    private static Material CreateMaterial(string materialName, Color color)
    {
        string path = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (color.a < 1f)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.renderQueue = 3000;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
