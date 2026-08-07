using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

namespace ZombieInfinite
{
    /// <summary>
    /// Streams authorable Terrain prefabs. A coordinate always resolves to the same prefab index.
    /// </summary>
    public sealed class ProceduralTerrainChunkManager : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private GameObject[] chunkPrefabs;
        [SerializeField, Min(0)] private int activeRadius = 1;
        [SerializeField] private int worldSeed = 2026;
        [SerializeField] private NavMeshSurface navMeshSurface;

        [Header("Chunk Streaming")]
        [Tooltip("Maximum number of new chunks instantiated or reused per frame.")]
        [SerializeField, Min(1)] private int chunksLoadedPerFrame = 1;

        [Header("Mini Map Layer")]
        [Tooltip("Layer used only by the Terrain root so MiniMapCamera can render ground without Player, Zombies, trees or rocks.")]
        [SerializeField, Range(0, 31)] private int minimapTerrainLayer = 6;

        [Header("Seeded Tree & Rock Props")]
        [Tooltip("Hide Terrain-painted tree instances without modifying TerrainData assets.")]
        [SerializeField] private bool hideTerrainPaintedTrees = true;
        [SerializeField] private GameObject[] treePrefabs;
        [SerializeField] private GameObject[] rockPrefabs;
        [SerializeField, Min(0)] private int treesPerChunk = 18;
        [SerializeField, Min(0)] private int rocksPerChunk = 10;
        [SerializeField, Min(0f)] private float placementMargin = 2f;
        [SerializeField, Range(0f, 90f)] private float maximumPlacementSlope = 38f;
        [SerializeField] private Vector2 treeScaleRange = new(0.85f, 1.25f);
        [SerializeField] private Vector2 rockScaleRange = new(0.75f, 1.35f);

        private readonly Dictionary<Vector2Int, ActiveChunk> activeChunks = new();
        private Queue<GameObject>[] pools;
        private float chunkSize;
        private Vector2Int currentCenter = new(int.MinValue, int.MinValue);
        private Vector2Int requestedCenter = new(int.MinValue, int.MinValue);
        private Coroutine rebuildRoutine;
        private Coroutine streamRoutine;
        private bool navMeshRebuildQueued;

        public bool IsInitialLoadComplete { get; private set; }

        private readonly struct ActiveChunk
        {
            public ActiveChunk(GameObject instance, int prefabIndex)
            {
                Instance = instance;
                PrefabIndex = prefabIndex;
            }

            public GameObject Instance { get; }
            public int PrefabIndex { get; }
            public Terrain Terrain => Instance.GetComponent<Terrain>();
        }

        public void Configure(Transform playerTransform, NavMeshSurface surface, GameObject[] prefabs)
        {
            player = playerTransform;
            navMeshSurface = surface;
            chunkPrefabs = prefabs;
        }

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            EnsurePools();
        }

        private void Start()
        {
            RefreshNow();
        }

        private void Update()
        {
            Vector2Int center = WorldToChunk(player.position);
            if (center != requestedCenter)
            {
                BeginStreaming(center);
            }
        }

        public int GetPrefabIndexForCoordinate(Vector2Int coordinate)
        {
            if (chunkPrefabs == null || chunkPrefabs.Length == 0)
            {
                return -1;
            }

            // Keep the demo origin easy to recognize and edit.
            if (coordinate == Vector2Int.zero)
            {
                return 0;
            }

            unchecked
            {
                uint hash = (uint)worldSeed * 83492791u;
                hash ^= (uint)coordinate.x * 73856093u;
                hash ^= (uint)coordinate.y * 19349663u;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                return (int)(hash % (uint)chunkPrefabs.Length);
            }
        }

        [ContextMenu("Refresh Chunks Now")]
        public void RefreshNow()
        {
            BeginStreaming(WorldToChunk(player.position));
        }

        private bool ValidateConfiguration()
        {
            if (player == null || chunkPrefabs == null || chunkPrefabs.Length == 0)
            {
                Debug.LogError("Terrain chunk manager requires a player and at least one Terrain prefab.", this);
                return false;
            }

            Terrain firstTerrain = chunkPrefabs[0] != null ? chunkPrefabs[0].GetComponent<Terrain>() : null;
            if (firstTerrain == null || firstTerrain.terrainData == null)
            {
                Debug.LogError("Every chunk prefab must contain a Terrain with TerrainData.", this);
                return false;
            }

            chunkSize = firstTerrain.terrainData.size.x;
            for (int i = 0; i < chunkPrefabs.Length; i++)
            {
                Terrain terrain = chunkPrefabs[i] != null ? chunkPrefabs[i].GetComponent<Terrain>() : null;
                if (terrain == null || terrain.terrainData == null)
                {
                    Debug.LogError($"Chunk prefab index {i} is missing TerrainData.", this);
                    return false;
                }

                Vector3 size = terrain.terrainData.size;
                if (!Mathf.Approximately(size.x, chunkSize) || !Mathf.Approximately(size.z, chunkSize))
                {
                    Debug.LogError("All chunk prefabs must use the same X/Z size so their borders align.", this);
                    return false;
                }
            }

            return true;
        }

        private void BeginStreaming(Vector2Int center)
        {
            requestedCenter = center;
            if (streamRoutine != null)
            {
                StopCoroutine(streamRoutine);
            }

            streamRoutine = StartCoroutine(StreamChunks(center));
        }

        private IEnumerator StreamChunks(Vector2Int center)
        {
            var required = new HashSet<Vector2Int>();
            for (int z = -activeRadius; z <= activeRadius; z++)
            {
                for (int x = -activeRadius; x <= activeRadius; x++)
                {
                    required.Add(center + new Vector2Int(x, z));
                }
            }

            var toAcquire = new List<Vector2Int>();
            foreach (Vector2Int coordinate in required)
            {
                if (!activeChunks.ContainsKey(coordinate))
                {
                    toAcquire.Add(coordinate);
                }
            }

            toAcquire.Sort((a, b) =>
                ((a - center).sqrMagnitude).CompareTo((b - center).sqrMagnitude));

            int loadedThisFrame = 0;
            foreach (Vector2Int coordinate in toAcquire)
            {
                if (center != requestedCenter)
                {
                    streamRoutine = null;
                    yield break;
                }

                activeChunks.Add(coordinate, AcquireChunk(coordinate));
                loadedThisFrame++;
                if (loadedThisFrame >= chunksLoadedPerFrame)
                {
                    loadedThisFrame = 0;
                    yield return null;
                }
            }

            var toRelease = new List<Vector2Int>();
            foreach (Vector2Int coordinate in activeChunks.Keys)
            {
                if (!required.Contains(coordinate))
                {
                    toRelease.Add(coordinate);
                }
            }

            foreach (Vector2Int coordinate in toRelease)
            {
                ActiveChunk released = activeChunks[coordinate];
                activeChunks.Remove(coordinate);
                released.Instance.SetActive(false);
                pools[released.PrefabIndex].Enqueue(released.Instance);
            }

            currentCenter = center;
            ConnectNeighbors();
            RequestNavMeshRebuild();
            streamRoutine = null;
        }

        private ActiveChunk AcquireChunk(Vector2Int coordinate)
        {
            EnsurePools();
            int prefabIndex = GetPrefabIndexForCoordinate(coordinate);
            GameObject instance = pools[prefabIndex].Count > 0
                ? pools[prefabIndex].Dequeue()
                : Instantiate(chunkPrefabs[prefabIndex], transform);

            instance.name = $"Chunk {coordinate.x}, {coordinate.y} [{chunkPrefabs[prefabIndex].name}]";
            instance.transform.SetParent(transform, false);
            instance.transform.position = new Vector3(coordinate.x * chunkSize, 0f, coordinate.y * chunkSize);
            instance.transform.rotation = Quaternion.identity;

            // Only the Terrain root uses MiniMapTerrain. Runtime tree/rock props keep
            // their normal prefab layers, so MiniMapCamera can cull them completely.
            instance.layer = minimapTerrainLayer;

            PrepareRuntimeTerrain(instance);
            RebuildSeededProps(instance, coordinate);
            instance.SetActive(true);
            return new ActiveChunk(instance, prefabIndex);
        }

        private void PrepareRuntimeTerrain(GameObject chunk)
        {
            Terrain terrain = chunk.GetComponent<Terrain>();
            TerrainCollider terrainCollider = chunk.GetComponent<TerrainCollider>();
            if (terrain == null || terrainCollider == null || terrain.terrainData == null)
            {
                return;
            }

            // Keep the original TerrainData so painted grass/detail layers remain.
            // Tree colliders are disabled in the prefab and treeDistance is set
            // to zero below, so no Terrain tree is rendered or used for physics.
            terrainCollider.terrainData = terrain.terrainData;
            terrainCollider.enabled = true;
        }

        private void RebuildSeededProps(GameObject chunk, Vector2Int coordinate)
        {
            Terrain terrain = chunk.GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null)
            {
                return;
            }

            if (hideTerrainPaintedTrees)
            {
                // TreeDistance belongs to this Terrain component, not the shared
                // TerrainData asset, so authored data remains untouched.
                terrain.treeDistance = 0f;
            }

            terrain.drawTreesAndFoliage = true;

            Transform previousRoot = chunk.transform.Find("Seeded Props");
            if (previousRoot != null)
            {
                previousRoot.gameObject.SetActive(false);
                Destroy(previousRoot.gameObject);
            }

            var rootObject = new GameObject("Seeded Props");
            Transform root = rootObject.transform;
            root.SetParent(chunk.transform, false);

            int coordinateSeed = GetCoordinateSeed(coordinate);
            var random = new System.Random(coordinateSeed);
            SpawnPropGroup(terrain, root, treePrefabs, treesPerChunk,
                treeScaleRange, random, "Tree");
            SpawnPropGroup(terrain, root, rockPrefabs, rocksPerChunk,
                rockScaleRange, random, "Rock");
        }

        private void SpawnPropGroup(
            Terrain terrain,
            Transform parent,
            GameObject[] prefabs,
            int requestedCount,
            Vector2 scaleRange,
            System.Random random,
            string label)
        {
            if (prefabs == null || prefabs.Length == 0 || requestedCount <= 0)
            {
                return;
            }

            Vector3 size = terrain.terrainData.size;
            float marginX = Mathf.Min(placementMargin, size.x * 0.45f);
            float marginZ = Mathf.Min(placementMargin, size.z * 0.45f);
            int spawned = 0;
            int attempts = Mathf.Max(8, requestedCount * 8);

            for (int attempt = 0; attempt < attempts && spawned < requestedCount; attempt++)
            {
                float localX = Mathf.Lerp(marginX, size.x - marginX, (float)random.NextDouble());
                float localZ = Mathf.Lerp(marginZ, size.z - marginZ, (float)random.NextDouble());
                float normalizedX = localX / size.x;
                float normalizedZ = localZ / size.z;
                Vector3 normal = terrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
                if (Vector3.Angle(normal, Vector3.up) > maximumPlacementSlope)
                {
                    continue;
                }

                Vector3 worldPosition = terrain.transform.position + new Vector3(localX, 0f, localZ);
                worldPosition.y = terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
                GameObject prefab = prefabs[random.Next(prefabs.Length)];
                if (prefab == null)
                {
                    continue;
                }

                GameObject prop = Instantiate(prefab, worldPosition,
                    Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), parent);
                prop.name = $"{label} {spawned:00} ({prefab.name})";
                float scale = Mathf.Lerp(
                    Mathf.Min(scaleRange.x, scaleRange.y),
                    Mathf.Max(scaleRange.x, scaleRange.y),
                    (float)random.NextDouble());
                prop.transform.localScale *= scale;
                EnsureCollider(prop);
                spawned++;
            }
        }

        private static void EnsureCollider(GameObject prop)
        {
            if (prop.GetComponentInChildren<Collider>() != null)
            {
                return;
            }

            MeshFilter meshFilter = prop.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            MeshCollider collider = meshFilter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
        }

        private int GetCoordinateSeed(Vector2Int coordinate)
        {
            unchecked
            {
                int hash = worldSeed;
                hash = hash * 397 ^ coordinate.x;
                hash = hash * 397 ^ coordinate.y;
                return hash;
            }
        }

        private void EnsurePools()
        {
            if (pools != null && pools.Length == chunkPrefabs.Length)
            {
                return;
            }

            pools = new Queue<GameObject>[chunkPrefabs.Length];
            for (int i = 0; i < pools.Length; i++)
            {
                pools[i] = new Queue<GameObject>();
            }
        }

        private Vector2Int WorldToChunk(Vector3 position)
        {
            return new Vector2Int(Mathf.FloorToInt(position.x / chunkSize), Mathf.FloorToInt(position.z / chunkSize));
        }

        private void ConnectNeighbors()
        {
            foreach ((Vector2Int coordinate, ActiveChunk chunk) in activeChunks)
            {
                Terrain left = activeChunks.TryGetValue(coordinate + Vector2Int.left, out ActiveChunk leftChunk) ? leftChunk.Terrain : null;
                Terrain top = activeChunks.TryGetValue(coordinate + Vector2Int.up, out ActiveChunk topChunk) ? topChunk.Terrain : null;
                Terrain right = activeChunks.TryGetValue(coordinate + Vector2Int.right, out ActiveChunk rightChunk) ? rightChunk.Terrain : null;
                Terrain bottom = activeChunks.TryGetValue(coordinate + Vector2Int.down, out ActiveChunk bottomChunk) ? bottomChunk.Terrain : null;
                chunk.Terrain.SetNeighbors(left, top, right, bottom);
            }
        }

        private void RequestNavMeshRebuild()
        {
            if (navMeshSurface == null)
            {
                return;
            }

            if (rebuildRoutine != null)
            {
                navMeshRebuildQueued = true;
                return;
            }

            rebuildRoutine = StartCoroutine(RebuildNavMeshNextFrame());
        }

        private IEnumerator RebuildNavMeshNextFrame()
        {
            do
            {
                navMeshRebuildQueued = false;
                yield return null;
                if (navMeshSurface.navMeshData == null)
                {
                    navMeshSurface.BuildNavMesh();
                }
                else
                {
                    AsyncOperation operation = navMeshSurface.UpdateNavMesh(
                        navMeshSurface.navMeshData);
                    while (!operation.isDone)
                    {
                        yield return null;
                    }
                }
            }
            while (navMeshRebuildQueued);

            IsInitialLoadComplete = true;
            rebuildRoutine = null;
        }

        private void OnValidate()
        {
            chunksLoadedPerFrame = Mathf.Max(1, chunksLoadedPerFrame);
            minimapTerrainLayer = Mathf.Clamp(minimapTerrainLayer, 0, 31);
        }
    }
}
