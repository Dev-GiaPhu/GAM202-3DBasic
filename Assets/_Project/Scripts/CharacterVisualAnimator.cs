using UnityEngine;

namespace ZombieInfinite
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterVisualAnimator : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField, Min(0f)] private float bobHeight = 0.045f;
        [SerializeField, Min(0f)] private float bobFrequency = 8f;
        [SerializeField, Min(0f)] private float leanAngle = 3f;

        private CharacterController controller;
        private Vector3 restPosition;
        private Quaternion restRotation;

        public void Configure(Transform visualTransform)
        {
            visual = visualTransform;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (visual != null)
            {
                restPosition = visual.localPosition;
                restRotation = visual.localRotation;
            }
        }

        private void LateUpdate()
        {
            if (visual == null)
            {
                return;
            }

            Vector3 planarVelocity = controller.velocity;
            planarVelocity.y = 0f;
            float movement = Mathf.Clamp01(planarVelocity.magnitude / 7f);
            float bob = Mathf.Sin(Time.time * bobFrequency) * bobHeight * movement;
            visual.localPosition = restPosition + Vector3.up * bob;
            visual.localRotation = restRotation * Quaternion.Euler(bob * leanAngle, 0f, -bob * leanAngle);
        }
    }
}
