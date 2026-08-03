using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieInfinite
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ZombieChaseReturn : MonoBehaviour
    {
        private enum EnemyState { WaitingForNavMesh, Idle, Chasing, Attacking, Returning }

        [SerializeField] private Transform player;
        [SerializeField, Min(0.1f)] private float detectionRadius = 5f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.25f;
        [SerializeField, Min(0f)] private float homeTolerance = 0.25f;
        [SerializeField, Min(1)] private int hitPoints = 3;

        private NavMeshAgent agent;
        private Vector3 homePosition;
        private EnemyState state;
        private bool dead;

        public string CurrentState => state.ToString();

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            homePosition = transform.position;
            state = EnemyState.WaitingForNavMesh;
        }

        public void Configure(Transform target)
        {
            player = target;
        }

        private void Update()
        {
            EvaluateBehavior();
        }

        public void EvaluateBehavior()
        {
            if (dead)
            {
                return;
            }

            if (player == null || !EnsureOnNavMesh())
            {
                return;
            }

            float distanceToPlayer = PlanarDistance(transform.position, player.position);
            if (distanceToPlayer <= attackRange)
            {
                state = EnemyState.Attacking;
                agent.isStopped = true;
                agent.ResetPath();
                return;
            }

            if (distanceToPlayer <= detectionRadius)
            {
                if (NavMesh.SamplePosition(player.position, out NavMeshHit targetHit, 8f, NavMesh.AllAreas))
                {
                    state = EnemyState.Chasing;
                    agent.isStopped = false;
                    agent.SetDestination(targetHit.position);
                }
                else
                {
                    state = EnemyState.WaitingForNavMesh;
                    agent.isStopped = true;
                    agent.ResetPath();
                }

                return;
            }

            float distanceHome = PlanarDistance(transform.position, homePosition);
            if (distanceHome > homeTolerance)
            {
                state = EnemyState.Returning;
                agent.isStopped = false;
                agent.SetDestination(homePosition);
            }
            else
            {
                state = EnemyState.Idle;
                agent.isStopped = true;
            }
        }

        public void TakeDamage(int amount)
        {
            if (dead)
            {
                return;
            }

            hitPoints -= Mathf.Max(0, amount);
            if (hitPoints <= 0)
            {
                dead = true;
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }

                GetComponent<ZombieAnimationDriver>()?.PlayDeath();
                StartCoroutine(DisableAfterDeath());
            }
        }

        private IEnumerator DisableAfterDeath()
        {
            yield return new WaitForSeconds(1.5f);
            gameObject.SetActive(false);
        }

        private static float PlanarDistance(Vector3 from, Vector3 to)
        {
            from.y = 0f;
            to.y = 0f;
            return Vector3.Distance(from, to);
        }

        private bool EnsureOnNavMesh()
        {
            if (!agent.enabled)
            {
                if (!NavMesh.SamplePosition(transform.position, out NavMeshHit initialHit, 8f, NavMesh.AllAreas))
                {
                    state = EnemyState.WaitingForNavMesh;
                    return false;
                }

                transform.position = initialHit.position;
                agent.enabled = true;
                agent.Warp(initialHit.position);
                homePosition = transform.position;
                state = EnemyState.Idle;
                return agent.isOnNavMesh;
            }

            if (agent.isOnNavMesh)
            {
                return true;
            }

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                state = EnemyState.Idle;
                return true;
            }

            state = EnemyState.WaitingForNavMesh;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(Application.isPlaying ? homePosition : transform.position, 0.2f);
        }
    }
}
