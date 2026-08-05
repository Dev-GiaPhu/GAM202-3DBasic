using System.Collections;
using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace ZombieInfinite
{
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class ZombieChaseReturn : MonoBehaviour
    {
        private enum EnemyState { WaitingForNavMesh, Idle, Chasing, Attacking, Returning }

        [Header("Target & AI")]
        [Tooltip("Player mà zombie sẽ phát hiện và đuổi theo.")]
        [SerializeField] private Transform player;

        [Tooltip("Bán kính phát hiện và đuổi theo Player.")]
        [SerializeField, Min(0.1f)] private float detectionRadius = 5f;

        [Tooltip("Khoảng cách bắt đầu tấn công.")]
        [SerializeField, Min(0.1f)] private float attackRange = 1.25f;

        [Tooltip("Khoảng cách được xem là đã trở về vị trí ban đầu.")]
        [SerializeField, Min(0f)] private float homeTolerance = 0.25f;

        [Header("Health & Combat")]
        [Tooltip("Máu tối đa của zombie.")]
        [FormerlySerializedAs("hitPoints")]
        [SerializeField, Min(1)] private int maxHealth = 3;

        [Tooltip("Sát thương gây cho Player mỗi lần đánh.")]
        [SerializeField, Min(0f)] private float attackDamage = 10f;

        [Tooltip("Thời gian chờ giữa hai lần gây sát thương.")]
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.1f;

        [Tooltip("Automatically add a hit collider to a Humanoid head bone when possible.")]
        [SerializeField] private bool createAutomaticHeadHitbox = true;

        [SerializeField, Min(0.01f)] private float automaticHeadHitboxRadius = 0.16f;

        [Header("Death")]
        [Tooltip("Thời gian giữ animation chết trước khi ẩn zombie.")]
        [SerializeField, Min(0f)] private float deathDelay = 1.5f;

        [Header("Loot Drops")]
        [Tooltip("Tỉ lệ rơi đạn: 0 = không rơi, 1 = luôn rơi.")]
        [SerializeField, Range(0f, 1f)] private float ammoDropChance = 0.35f;

        [Tooltip("Số pickup đạn nhận được khi nhặt.")]
        [SerializeField, Min(1)] private int ammoPickupAmount = 1;

        [Tooltip("Tỉ lệ rơi vật phẩm hồi máu: 0 = không rơi, 1 = luôn rơi.")]
        [SerializeField, Range(0f, 1f)] private float medkitDropChance = 0.18f;

        [Tooltip("Số medkit nhận được khi nhặt.")]
        [SerializeField, Min(1)] private int medkitPickupAmount = 1;

        private NavMeshAgent agent;
        private Vector3 homePosition;
        private EnemyState state;
        private bool dead;
        private int currentHitPoints;
        private float nextAttackTime;
        private bool provoked;

        public string CurrentState => state.ToString();
        public bool IsDead => dead;
        public int CurrentHealth => currentHitPoints;
        public int MaxHealth => maxHealth;
        public float HealthNormalized => maxHealth > 0
            ? Mathf.Clamp01((float)currentHitPoints / maxHealth)
            : 0f;
        public event Action<int, int> HealthChanged;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            homePosition = transform.position;
            state = EnemyState.WaitingForNavMesh;
            currentHitPoints = maxHealth;
            EnsureHeadHitZone();

            if (GetComponent<ZombieWorldHealthBar>() == null)
            {
                gameObject.AddComponent<ZombieWorldHealthBar>();
            }
        }

        public void Configure(Transform target)
        {
            player = target;
        }

        public void Configure(Transform target, float chaseRadius)
        {
            player = target;
            detectionRadius = Mathf.Max(0.1f, chaseRadius);
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

                if (Time.time >= nextAttackTime)
                {
                    nextAttackTime = Time.time + attackCooldown;
                    player.GetComponent<PlayerSurvivalStats>()?.TakeDamage(attackDamage);
                }

                return;
            }

            if (provoked || distanceToPlayer <= detectionRadius)
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
            TakeDamage(amount, null, false);
        }

        public void TakeDamage(int amount, Transform attacker, bool instantKill)
        {
            if (dead)
            {
                return;
            }

            if (attacker != null)
            {
                AlertByGunshot(attacker);
            }

            currentHitPoints = instantKill
                ? 0
                : Mathf.Clamp(currentHitPoints - Mathf.Max(0, amount), 0, maxHealth);

            HealthChanged?.Invoke(currentHitPoints, maxHealth);

            GetComponent<ZombieWorldHealthBar>()?.ShowTemporarily();
            if (currentHitPoints <= 0)
            {
                dead = true;
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }

                GetComponent<ZombieAnimationDriver>()?.PlayDeath();
                DropLoot();
                StartCoroutine(DisableAfterDeath());
            }
        }

        /// <summary>
        /// A gunshot permanently provokes this zombie for its current life,
        /// even when the player is outside the normal detection radius.
        /// </summary>
        public void AlertByGunshot(Transform attacker)
        {
            if (dead || attacker == null)
            {
                return;
            }

            player = attacker;
            provoked = true;
        }

        private void DropLoot()
        {
            if (Random.value <= ammoDropChance)
            {
                CreatePickup(SurvivalPickup.PickupType.Ammo, ammoPickupAmount,
                    transform.position + Vector3.up * 0.45f + transform.right * 0.35f);
            }

            if (Random.value <= medkitDropChance)
            {
                CreatePickup(SurvivalPickup.PickupType.Medkit, medkitPickupAmount,
                    transform.position + Vector3.up * 0.45f - transform.right * 0.35f);
            }
        }

        private void EnsureHeadHitZone()
        {
            if (!createAutomaticHeadHitbox)
            {
                return;
            }

            Animator animator = GetComponentInChildren<Animator>();
            Transform head = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : FindNamedHead(transform);
            if (head == null)
            {
                return;
            }

            ZombieHitZone zone = head.GetComponent<ZombieHitZone>() ??
                head.gameObject.AddComponent<ZombieHitZone>();
            zone.Configure(ZombieHitZone.Zone.Head);

            if (head.GetComponent<Collider>() == null)
            {
                SphereCollider headCollider = head.gameObject.AddComponent<SphereCollider>();
                headCollider.radius = automaticHeadHitboxRadius;
                headCollider.isTrigger = false;
            }
        }

        private static Transform FindNamedHead(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                string lowerName = child.name.ToLowerInvariant();
                if (lowerName.Contains("head") || lowerName.Contains("skull"))
                {
                    return child;
                }
            }

            return null;
        }

        private static void CreatePickup(
            SurvivalPickup.PickupType type,
            int amount,
            Vector3 position)
        {
            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pickup.name = type == SurvivalPickup.PickupType.Ammo
                ? "Ammo Pickup"
                : "Medkit Pickup";
            pickup.transform.position = position;
            pickup.transform.localScale = Vector3.one * 0.45f;
            Rigidbody body = pickup.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            SurvivalPickup component = pickup.AddComponent<SurvivalPickup>();
            component.Configure(type, amount);
        }

        private IEnumerator DisableAfterDeath()
        {
            yield return new WaitForSeconds(deathDelay);
            Destroy(gameObject);
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

        private void OnValidate()
        {
            detectionRadius = Mathf.Max(0.1f, detectionRadius);
            attackRange = Mathf.Max(0.1f, attackRange);
            homeTolerance = Mathf.Max(0f, homeTolerance);
            maxHealth = Mathf.Max(1, maxHealth);
            attackDamage = Mathf.Max(0f, attackDamage);
            attackCooldown = Mathf.Max(0.1f, attackCooldown);
            automaticHeadHitboxRadius = Mathf.Max(0.01f, automaticHeadHitboxRadius);
            deathDelay = Mathf.Max(0f, deathDelay);
            ammoDropChance = Mathf.Clamp01(ammoDropChance);
            ammoPickupAmount = Mathf.Max(1, ammoPickupAmount);
            medkitDropChance = Mathf.Clamp01(medkitDropChance);
            medkitPickupAmount = Mathf.Max(1, medkitPickupAmount);
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
