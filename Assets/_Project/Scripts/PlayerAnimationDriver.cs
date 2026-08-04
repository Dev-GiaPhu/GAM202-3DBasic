using UnityEngine;

namespace ZombieInfinite
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Player_Idle");
        private static readonly int RunState = Animator.StringToHash("Player_Run");

        [SerializeField] private Animator animator;

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
            animator.SetFloat("Speed", velocity.magnitude);
        }
    }
}
