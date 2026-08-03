using UnityEngine;
using UnityEngine.AI;

namespace ZombieInfinite
{
    [RequireComponent(typeof(ZombieChaseReturn), typeof(NavMeshAgent))]
    public sealed class ZombieAnimationDriver : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Z_Idle");
        private static readonly int WalkState = Animator.StringToHash("Z_Walk_InPlace");
        private static readonly int AttackState = Animator.StringToHash("Z_Attack");
        private static readonly int DeathState = Animator.StringToHash("Z_FallingBack");

        [SerializeField] private Animator animator;
        private ZombieChaseReturn behavior;
        private NavMeshAgent agent;
        private int currentState;
        private bool dead;

        public void Configure(Animator targetAnimator)
        {
            animator = targetAnimator;
        }

        private void Awake()
        {
            behavior = GetComponent<ZombieChaseReturn>();
            agent = GetComponent<NavMeshAgent>();
        }

        private void Update()
        {
            if (dead || animator == null)
            {
                return;
            }

            bool chasing = behavior.CurrentState == "Chasing";
            bool returning = behavior.CurrentState == "Returning";
            bool attacking = behavior.CurrentState == "Attacking";
            Play(attacking ? AttackState : chasing || returning ? WalkState : IdleState, 0.12f);
        }

        public void PlayDeath()
        {
            dead = true;
            Play(DeathState, 0.08f);
        }

        private void Play(int state, float fadeDuration)
        {
            if (animator == null || currentState == state)
            {
                return;
            }

            currentState = state;
            animator.CrossFade(state, fadeDuration);
        }
    }
}
