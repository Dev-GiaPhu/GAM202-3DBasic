using UnityEngine;

namespace ZombieInfinite
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Player_Idle");
        private static readonly int RunState = Animator.StringToHash("Player_Run");

        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float movementThreshold = 0.1f;

        private CharacterController controller;
        private int currentState;

        public void Configure(Animator targetAnimator)
        {
            animator = targetAnimator;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            Play(velocity.sqrMagnitude > movementThreshold * movementThreshold ? RunState : IdleState);
        }

        private void Play(int state)
        {
            if (currentState == state)
            {
                return;
            }

            currentState = state;
            animator.CrossFade(state, 0.12f);
        }
    }
}
