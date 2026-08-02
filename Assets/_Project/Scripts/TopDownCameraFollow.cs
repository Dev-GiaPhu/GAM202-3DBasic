using UnityEngine;

namespace ZombieInfinite
{
    public sealed class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(0f, 18f, -10.4f);
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;
        private Vector3 velocity;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref velocity, smoothTime);
            transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        }
    }
}
