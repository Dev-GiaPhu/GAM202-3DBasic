using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieInfinite
{
    public sealed class ZombieSpawner : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private GameObject[] zombiePrefabs;
        [SerializeField, Min(1f)] private float spawnRadius = 22f;
        [SerializeField, Min(0f)] private float minimumSpawnDistance = 10f;
        [SerializeField, Min(1)] private int maximumAlive = 12;
        [SerializeField, Min(0.1f)] private float spawnInterval = 3f;
        [SerializeField, Min(0.1f)] private float zombieChaseRadius = 12f;
        [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 8f;

        private readonly List<ZombieChaseReturn> alive = new();
        private float nextSpawnTime;

        private void Update()
        {
            alive.RemoveAll(zombie => zombie == null || zombie.IsDead || !zombie.gameObject.activeInHierarchy);

            if (player == null || zombiePrefabs == null || zombiePrefabs.Length == 0 ||
                alive.Count >= maximumAlive || Time.time < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = Time.time + spawnInterval;
            TrySpawn();
        }

        private void TrySpawn()
        {
            Vector2 circle = Random.insideUnitCircle.normalized;
            if (circle.sqrMagnitude < 0.001f)
            {
                circle = Vector2.right;
            }
            float distance = Random.Range(
                Mathf.Min(minimumSpawnDistance, spawnRadius),
                Mathf.Max(minimumSpawnDistance, spawnRadius));
            Vector3 candidate = player.position + new Vector3(circle.x, 0f, circle.y) * distance;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                    navMeshSampleDistance, NavMesh.AllAreas))
            {
                return;
            }

            GameObject prefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];
            if (prefab == null)
            {
                return;
            }

            GameObject instance = Instantiate(prefab, hit.position, Quaternion.identity);
            ZombieChaseReturn zombie = instance.GetComponent<ZombieChaseReturn>();
            if (zombie == null)
            {
                Destroy(instance);
                return;
            }

            zombie.Configure(player, zombieChaseRadius);
            alive.Add(zombie);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = player != null ? player.position : transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, spawnRadius);
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(center, minimumSpawnDistance);
        }

        private void OnValidate()
        {
            spawnRadius = Mathf.Max(1f, spawnRadius);
            minimumSpawnDistance = Mathf.Clamp(minimumSpawnDistance, 0f, spawnRadius);
            maximumAlive = Mathf.Max(1, maximumAlive);
            spawnInterval = Mathf.Max(0.1f, spawnInterval);
            zombieChaseRadius = Mathf.Max(0.1f, zombieChaseRadius);
            navMeshSampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);
        }
    }
}
