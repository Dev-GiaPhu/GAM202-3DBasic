using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
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
    private const string AnimationFolder = "Assets/_Project/Animation";
    private const string MutantPrefabPath = "Assets/ThirdParty/Biomechanical_Mutant/Prefabs/Biomech_Mutant_Skin_1.prefab";
    private const string ZombieVisualPrefabPath = "Assets/ThirdParty/Zombie/Prefabs/Zombie1.prefab";

    [MenuItem("Tools/Zombie Infinite/Build Demo Scene")]
    public static void Build()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);
        EnsureFolder(ChunkPrefabFolder);
        EnsureFolder(ChunkDataFolder);
        EnsureFolder(TerrainLayerFolder);
        EnsureFolder(AnimationFolder);

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

        RuntimeAnimatorController zombieController = CreateZombieController();

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

        GameObject zombie = BuildZombie(zombieMaterial, zombieController);
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
        CharacterController characterController = root.AddComponent<CharacterController>();
        characterController.height = 2f;
        characterController.radius = 0.45f;
        characterController.center = new Vector3(0f, 1f, 0f);

        GameObject visual = InstantiateVisual(MutantPrefabPath, root.transform, "Mutant Visual");
        if (visual != null)
        {
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = CreatePlayerController(animator.transform);
                animator.applyRootMotion = false;
                root.AddComponent<PlayerAnimationDriver>().Configure(animator);
            }
            else
            {
                Debug.LogWarning("Mutant visual does not contain an Animator component.");
            }
        }
        else
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Fallback Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up;
            body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            Object.DestroyImmediate(body.GetComponent<Collider>());
        }

        GameObject gun = BuildGun(root.transform, gunMaterial);
        root.AddComponent<TopDownPlayerController>().Configure(gun.transform.Find("Muzzle"));
        return root;
    }

    private static GameObject BuildZombie(Material material, RuntimeAnimatorController controller)
    {
        GameObject root = new("Zombie");
        root.name = "Zombie";
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.height = 2f;
        collider.radius = 0.5f;
        collider.center = Vector3.up;

        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        agent.speed = 3.5f;
        agent.angularSpeed = 720f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 1.25f;
        root.AddComponent<ZombieChaseReturn>();

        GameObject visual = InstantiateVisual(ZombieVisualPrefabPath, root.transform, "Zombie Visual");
        Animator animator = visual != null ? visual.GetComponentInChildren<Animator>() : null;
        if (animator != null)
        {
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            root.AddComponent<ZombieAnimationDriver>().Configure(animator);
        }
        else
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = "Fallback Body";
            fallback.transform.SetParent(root.transform, false);
            fallback.transform.localPosition = Vector3.up;
            fallback.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(fallback.GetComponent<Collider>());
        }

        return root;
    }

    private static GameObject BuildGun(Transform parent, Material material)
    {
        GameObject root = new("Gun");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0.5f, 1.25f, 0.65f);

        CreateGunPart("Receiver", root.transform, new Vector3(0f, 0f, 0.15f), new Vector3(0.22f, 0.18f, 0.65f), material);
        CreateGunPart("Barrel", root.transform, new Vector3(0f, 0f, 0.72f), new Vector3(0.08f, 0.08f, 0.65f), material);
        GameObject grip = CreateGunPart("Grip", root.transform, new Vector3(0f, -0.18f, 0.05f), new Vector3(0.12f, 0.35f, 0.14f), material);
        grip.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);

        GameObject muzzle = new("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0f, 1.05f);
        return root;
    }

    private static GameObject CreateGunPart(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        return part;
    }

    private static GameObject InstantiateVisual(string assetPath, Transform parent, string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Demo visual prefab was not found at {assetPath}.");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        return instance;
    }

    private static RuntimeAnimatorController CreateZombieController()
    {
        const string controllerPath = AnimationFolder + "/ZombieDemo.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ChildAnimatorState[] oldStates = stateMachine.states;
        foreach (ChildAnimatorState oldState in oldStates)
        {
            stateMachine.RemoveState(oldState.state);
        }

        AnimatorState idle = AddAnimationState(stateMachine, "Z_Idle", "Assets/ThirdParty/Zombie/Animations/Z_Idle.anim", new Vector3(250f, 50f));
        AddAnimationState(stateMachine, "Z_Walk_InPlace", "Assets/ThirdParty/Zombie/Animations/Z_Walk_InPlace.anim", new Vector3(250f, 120f));
        AddAnimationState(stateMachine, "Z_Attack", "Assets/ThirdParty/Zombie/Animations/Z_Attack.anim", new Vector3(250f, 190f));
        AddAnimationState(stateMachine, "Z_FallingBack", "Assets/ThirdParty/Zombie/Animations/Z_FallingBack.anim", new Vector3(250f, 260f));
        stateMachine.defaultState = idle;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static RuntimeAnimatorController CreatePlayerController(Transform skeletonRoot)
    {
        AnimationClip idleClip = CreateMutantIdleClip(skeletonRoot);
        AnimationClip runClip = CreateMutantRunClip(skeletonRoot);
        const string controllerPath = AnimationFolder + "/PlayerDemo.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ChildAnimatorState[] oldStates = stateMachine.states;
        foreach (ChildAnimatorState oldState in oldStates)
        {
            stateMachine.RemoveState(oldState.state);
        }

        AnimatorState idle = AddAnimationState(stateMachine, "Player_Idle", idleClip, new Vector3(250f, 50f));
        AddAnimationState(stateMachine, "Player_Run", runClip, new Vector3(250f, 120f));
        stateMachine.defaultState = idle;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimationClip CreateMutantIdleClip(Transform skeletonRoot)
    {
        AnimationClip clip = CreateOrClearClip(AnimationFolder + "/Mutant_Idle.anim", "Mutant_Idle", 2f);
        SetBoneRotation(clip, skeletonRoot, "spine_03", 2f,
            Vector3.zero, new Vector3(2f, 0f, 0f), Vector3.zero);
        SetBoneRotation(clip, skeletonRoot, "upperarm_l", 2f,
            Vector3.zero, new Vector3(0f, 0f, -2f), Vector3.zero);
        SetBoneRotation(clip, skeletonRoot, "upperarm_r", 2f,
            Vector3.zero, new Vector3(0f, 0f, 2f), Vector3.zero);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip CreateMutantRunClip(Transform skeletonRoot)
    {
        const float duration = 0.8f;
        AnimationClip clip = CreateOrClearClip(AnimationFolder + "/Mutant_Run.anim", "Mutant_Run", duration);
        SetBoneRotation(clip, skeletonRoot, "thigh_l", duration,
            Vector3.zero, new Vector3(34f, 0f, 0f), Vector3.zero, new Vector3(-34f, 0f, 0f), Vector3.zero);
        SetBoneRotation(clip, skeletonRoot, "thigh_r", duration,
            Vector3.zero, new Vector3(-34f, 0f, 0f), Vector3.zero, new Vector3(34f, 0f, 0f), Vector3.zero);
        SetBoneRotation(clip, skeletonRoot, "calf_l", duration,
            Vector3.zero, new Vector3(-8f, 0f, 0f), new Vector3(-30f, 0f, 0f), Vector3.zero, Vector3.zero);
        SetBoneRotation(clip, skeletonRoot, "calf_r", duration,
            new Vector3(-30f, 0f, 0f), Vector3.zero, Vector3.zero, new Vector3(-8f, 0f, 0f), new Vector3(-30f, 0f, 0f));
        SetBoneRotation(clip, skeletonRoot, "upperarm_l", duration,
            Vector3.zero, new Vector3(-24f, 0f, 0f), Vector3.zero, new Vector3(24f, 0f, 0f), Vector3.zero);
        SetBoneRotation(clip, skeletonRoot, "upperarm_r", duration,
            Vector3.zero, new Vector3(24f, 0f, 0f), Vector3.zero, new Vector3(-24f, 0f, 0f), Vector3.zero);
        SetBoneRotation(clip, skeletonRoot, "spine_03", duration,
            Vector3.zero, new Vector3(3f, -4f, 0f), Vector3.zero, new Vector3(3f, 4f, 0f), Vector3.zero);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip CreateOrClearClip(string assetPath, string clipName, float duration)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, assetPath);
        }
        else
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        clip.name = clipName;
        clip.frameRate = 30f;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.loopBlend = true;
        settings.stopTime = duration;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static void SetBoneRotation(AnimationClip clip, Transform skeletonRoot, string boneName,
        float duration, params Vector3[] eulerOffsets)
    {
        Transform bone = FindChildRecursive(skeletonRoot, boneName);
        if (bone == null)
        {
            Debug.LogWarning($"Mutant animation bone '{boneName}' was not found.");
            return;
        }

        string path = AnimationUtility.CalculateTransformPath(bone, skeletonRoot);
        Quaternion rest = bone.localRotation;
        var x = new Keyframe[eulerOffsets.Length];
        var y = new Keyframe[eulerOffsets.Length];
        var z = new Keyframe[eulerOffsets.Length];
        var w = new Keyframe[eulerOffsets.Length];
        for (int index = 0; index < eulerOffsets.Length; index++)
        {
            float time = duration * index / (eulerOffsets.Length - 1f);
            Quaternion rotation = rest * Quaternion.Euler(eulerOffsets[index]);
            x[index] = new Keyframe(time, rotation.x);
            y[index] = new Keyframe(time, rotation.y);
            z[index] = new Keyframe(time, rotation.z);
            w[index] = new Keyframe(time, rotation.w);
        }

        clip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", new AnimationCurve(x));
        clip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", new AnimationCurve(y));
        clip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", new AnimationCurve(z));
        clip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", new AnimationCurve(w));
        clip.EnsureQuaternionContinuity();
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static AnimatorState AddAnimationState(AnimatorStateMachine stateMachine, string stateName, string clipPath, Vector3 position)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        AnimatorState state = stateMachine.AddState(stateName, position);
        state.motion = clip;
        if (clip == null)
        {
            Debug.LogWarning($"Animation clip was not found at {clipPath}.");
        }

        return state;
    }

    private static AnimatorState AddAnimationState(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = stateMachine.AddState(stateName, position);
        state.motion = clip;
        return state;
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
