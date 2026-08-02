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

        private readonly Dictionary<Vector2Int, ActiveChunk> activeChunks = new();
        private Queue<GameObject>[] pools;
        private float chunkSize;
        private Vector2Int currentCenter = new(int.MinValue, int.MinValue);
        private Coroutine rebuildRoutine;

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
            RefreshChunks(force: false);
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
            RefreshChunks(force: true);
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

        private void RefreshChunks(bool force)
        {
            Vector2Int center = WorldToChunk(player.position);
            if (!force && center == currentCenter)
            {
                return;
            }

            currentCenter = center;
            var required = new HashSet<Vector2Int>();
            for (int z = -activeRadius; z <= activeRadius; z++)
            {
                for (int x = -activeRadius; x <= activeRadius; x++)
                {
                    required.Add(center + new Vector2Int(x, z));
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

            foreach (Vector2Int coordinate in required)
            {
                if (!activeChunks.ContainsKey(coordinate))
                {
                    activeChunks.Add(coordinate, AcquireChunk(coordinate));
                }
            }

            ConnectNeighbors();
            RequestNavMeshRebuild();
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
            instance.SetActive(true);
            return new ActiveChunk(instance, prefabIndex);
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
                StopCoroutine(rebuildRoutine);
            }

            rebuildRoutine = StartCoroutine(RebuildNavMeshNextFrame());
        }

        private IEnumerator RebuildNavMeshNextFrame()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            navMeshSurface.RemoveData();
            navMeshSurface.BuildNavMesh();
            rebuildRoutine = null;
        }
    }
}
