using UnityEngine;
using UnityEngine.AI;

namespace ZombieInfinite
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ZombieChaseReturn : MonoBehaviour
    {
        private enum EnemyState { WaitingForNavMesh, Idle, Chasing, Returning }

        [SerializeField] private Transform player;
        [SerializeField, Min(0.1f)] private float detectionRadius = 5f;
        [SerializeField, Min(0f)] private float homeTolerance = 0.25f;
        [SerializeField, Min(1)] private int hitPoints = 3;

        private NavMeshAgent agent;
        private Vector3 homePosition;
        private EnemyState state;

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
            if (player == null || !EnsureOnNavMesh())
            {
                return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= detectionRadius)
            {
                state = EnemyState.Chasing;
                agent.isStopped = false;
                agent.SetDestination(player.position);
                return;
            }

            float distanceHome = Vector3.Distance(transform.position, homePosition);
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
            hitPoints -= Mathf.Max(0, amount);
            if (hitPoints <= 0)
            {
                gameObject.SetActive(false);
            }
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
