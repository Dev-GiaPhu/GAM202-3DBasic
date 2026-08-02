using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieInfinite
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class TopDownPlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 7f;
        [SerializeField, Min(0f)] private float turnSpeed = 900f;
        [SerializeField, Min(0f)] private float fireRate = 8f;
        [SerializeField, Min(0f)] private float weaponRange = 60f;
        [SerializeField] private Transform muzzle;
        [SerializeField] private LayerMask aimMask = ~0;

        private CharacterController controller;
        private Camera gameplayCamera;
        private float nextShotTime;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            gameplayCamera = Camera.main;
        }

        private void Update()
        {
            if (Keyboard.current == null || Mouse.current == null)
            {
                return;
            }

            Move();
            Aim();

            if (Mouse.current.leftButton.isPressed && Time.time >= nextShotTime)
            {
                nextShotTime = Time.time + 1f / fireRate;
                Fire();
            }
        }

        public void Configure(Transform muzzleTransform)
        {
            muzzle = muzzleTransform;
        }

        private void Move()
        {
            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;

            Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;
            controller.SimpleMove(direction * moveSpeed);
        }

        private void Aim()
        {
            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
                if (gameplayCamera == null) return;
            }

            Ray ray = gameplayCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (!plane.Raycast(ray, out float distance))
            {
                return;
            }

            Vector3 lookDirection = ray.GetPoint(distance) - transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }
        }

        private void Fire()
        {
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up;
            Vector3 direction = transform.forward;
            Vector3 end = origin + direction * weaponRange;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, weaponRange, aimMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                if (hit.collider.TryGetComponent(out ZombieChaseReturn zombie))
                {
                    zombie.TakeDamage(1);
                }
            }

            StartCoroutine(DrawTracer(origin, end));
        }

        private IEnumerator DrawTracer(Vector3 start, Vector3 end)
        {
            var tracer = new GameObject("Shot Tracer");
            var line = tracer.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = 0.05f;
            line.endWidth = 0.015f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = Color.yellow;
            line.endColor = new Color(1f, 0.2f, 0f, 0.2f);
            yield return new WaitForSeconds(0.06f);
            Destroy(tracer);
        }
    }
}
