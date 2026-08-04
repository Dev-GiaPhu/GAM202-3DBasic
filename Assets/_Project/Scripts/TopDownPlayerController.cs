using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieInfinite
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class TopDownPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)]
        private float moveSpeed = 7f;

        [Header("Weapon References")]
        [Tooltip("Object cha dùng để xoay toàn bộ cây súng theo tâm ngắm.")]
        [SerializeField]
        private Transform weaponAimPivot;

        [Tooltip("Điểm đặt ngay ngoài đầu nòng súng.")]
        [SerializeField]
        private Transform firePoint;

        [Header("Weapon Settings")]
        [SerializeField, Min(0.01f)]
        private float fireRate = 8f;

        [SerializeField, Min(0f)]
        private float weaponRange = 60f;

        [SerializeField]
        private LayerMask aimMask = ~0;

        [Header("Weapon Aim")]
        [Tooltip("Tốc độ súng xoay theo tâm ngắm.")]
        [SerializeField, Min(0f)]
        private float weaponAimSpeed = 25f;

        [Tooltip("Khoảng cách tối thiểu của điểm ngắm.")]
        [SerializeField, Min(0.1f)]
        private float minimumAimDistance = 2f;

        [Header("Camera")]
        [Tooltip("Main Camera có Cinemachine Brain.")]
        [SerializeField]
        private Camera gameplayCamera;

        [Tooltip("Script quản lý chuyển FPS/TPS.")]
        [SerializeField]
        private TopDownCameraFollow cameraViewController;

        [Header("Tracer")]
        [SerializeField]
        private Material tracerMaterial;

        [SerializeField, Min(0f)]
        private float tracerDuration = 0.06f;

        [SerializeField, Min(0f)]
        private float tracerStartWidth = 0.035f;

        [SerializeField, Min(0f)]
        private float tracerEndWidth = 0.01f;

        [Tooltip("Đẩy điểm hiển thị tracer ra trước nòng ở góc FPS.")]
        [SerializeField, Min(0f)]
        private float firstPersonTracerOffset = 0.35f;

        [Tooltip("Ở góc TPS có thể để tracer bắt đầu ngay đầu nòng.")]
        [SerializeField, Min(0f)]
        private float thirdPersonTracerOffset = 0.02f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[32];

        private CharacterController controller;
        private Material runtimeTracerMaterial;

        private Vector3 currentAimPoint;
        private float nextShotTime;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            EnsureReferences();
            CreateRuntimeTracerMaterial();
        }

        private void Start()
        {
            ValidateReferences();
        }

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            EnsureReferences();

            Move();
            UpdateAimPoint();
            RotateWeaponTowardsAimPoint();

            if (Mouse.current == null ||
                Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            if (Mouse.current.leftButton.isPressed &&
                Time.time >= nextShotTime)
            {
                nextShotTime =
                    Time.time + 1f / Mathf.Max(0.01f, fireRate);

                Fire();
            }
        }

        public void Configure(Transform firePointTransform)
        {
            firePoint = firePointTransform;
        }

        public void ConfigureWeapon(
            Transform weaponPivotTransform,
            Transform firePointTransform)
        {
            weaponAimPivot = weaponPivotTransform;
            firePoint = firePointTransform;
        }

        private void Move()
        {
            Vector2 input = Vector2.zero;

            if (Keyboard.current.wKey.isPressed)
            {
                input.y += 1f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                input.y -= 1f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                input.x += 1f;
            }

            if (Keyboard.current.aKey.isPressed)
            {
                input.x -= 1f;
            }

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            Vector3 cameraForward = transform.forward;
            Vector3 cameraRight = transform.right;

            if (gameplayCamera != null)
            {
                cameraForward = gameplayCamera.transform.forward;
                cameraRight = gameplayCamera.transform.right;
            }

            cameraForward.y = 0f;
            cameraRight.y = 0f;

            if (cameraForward.sqrMagnitude < 0.001f)
            {
                cameraForward = transform.forward;
            }

            if (cameraRight.sqrMagnitude < 0.001f)
            {
                cameraRight = transform.right;
            }

            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 moveDirection =
                cameraForward * input.y +
                cameraRight * input.x;

            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            controller.SimpleMove(moveDirection * moveSpeed);
        }

        private void UpdateAimPoint()
        {
            if (gameplayCamera == null)
            {
                currentAimPoint =
                    transform.position +
                    transform.forward * weaponRange;

                return;
            }

            // Ray đi chính xác qua tâm màn hình.
            Ray cameraRay = gameplayCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));

            currentAimPoint =
                cameraRay.origin +
                cameraRay.direction * weaponRange;

            if (TryGetClosestHit(
                    cameraRay.origin,
                    cameraRay.direction,
                    weaponRange,
                    out RaycastHit cameraHit))
            {
                currentAimPoint = cameraHit.point;
            }

            // Tránh trường hợp điểm ngắm quá gần súng khiến súng xoay ngược.
            Vector3 fromPlayerToAim =
                currentAimPoint - transform.position;

            if (fromPlayerToAim.sqrMagnitude <
                minimumAimDistance * minimumAimDistance)
            {
                currentAimPoint =
                    cameraRay.origin +
                    cameraRay.direction * minimumAimDistance;
            }
        }

        private void RotateWeaponTowardsAimPoint()
        {
            if (weaponAimPivot == null || firePoint == null)
            {
                return;
            }

            Vector3 aimDirection =
                currentAimPoint - firePoint.position;

            if (aimDirection.sqrMagnitude < 0.001f)
            {
                return;
            }

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    aimDirection.normalized,
                    transform.up);

            if (weaponAimSpeed <= 0f)
            {
                weaponAimPivot.rotation = targetRotation;
                return;
            }

            float interpolation =
                1f - Mathf.Exp(
                    -weaponAimSpeed * Time.deltaTime);

            weaponAimPivot.rotation =
                Quaternion.Slerp(
                    weaponAimPivot.rotation,
                    targetRotation,
                    interpolation);
        }

        private void Fire()
        {
            if (gameplayCamera == null || firePoint == null)
            {
                return;
            }

            // Điểm bắt đầu raycast thật sự:
            // luôn chính xác tại FirePoint.
            Vector3 shotOrigin = firePoint.position;

            Vector3 shotDirection =
                currentAimPoint - shotOrigin;

            if (shotDirection.sqrMagnitude < 0.001f)
            {
                shotDirection = firePoint.forward;
            }
            else
            {
                shotDirection.Normalize();
            }

            Vector3 shotEnd =
                shotOrigin +
                shotDirection * weaponRange;

            // Raycast gây sát thương xuất phát từ FirePoint.
            if (TryGetClosestHit(
                    shotOrigin,
                    shotDirection,
                    weaponRange,
                    out RaycastHit weaponHit))
            {
                shotEnd = weaponHit.point;

                ZombieChaseReturn zombie =
                    weaponHit.collider.GetComponentInParent
                        <ZombieChaseReturn>();

                if (zombie != null)
                {
                    zombie.TakeDamage(1);
                }
            }

            // Tracer chỉ được đẩy ra ngoài model súng về mặt hình ảnh.
            // Raycast thật vẫn bắt đầu tại FirePoint.
            float tracerOffset = IsFirstPerson()
                ? firstPersonTracerOffset
                : thirdPersonTracerOffset;

            Vector3 tracerStart =
                shotOrigin +
                shotDirection * tracerOffset;

            // Nếu mục tiêu quá gần thì không để tracer vượt qua mục tiêu.
            float shotDistance =
                Vector3.Distance(shotOrigin, shotEnd);

            if (tracerOffset >= shotDistance)
            {
                tracerStart = shotOrigin;
            }

            StartCoroutine(
                DrawTracer(tracerStart, shotEnd));
        }

        private bool IsFirstPerson()
        {
            return cameraViewController != null &&
                   cameraViewController.IsFirstPerson;
        }

        private bool TryGetClosestHit(
            Vector3 origin,
            Vector3 direction,
            float distance,
            out RaycastHit closestHit)
        {
            closestHit = default;

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                hitBuffer,
                distance,
                aimMask,
                QueryTriggerInteraction.Ignore);

            bool foundHit = false;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hitBuffer[i];

                if (hit.collider == null)
                {
                    continue;
                }

                Transform hitTransform =
                    hit.collider.transform;

                // Bỏ qua collider của Player và vũ khí.
                if (hitTransform == transform ||
                    hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestHit = hit;
                foundHit = true;
            }

            return foundHit;
        }

        private IEnumerator DrawTracer(
            Vector3 start,
            Vector3 end)
        {
            var tracerObject =
                new GameObject("Shot Tracer");

            LineRenderer line =
                tracerObject.AddComponent<LineRenderer>();

            line.useWorldSpace = true;
            line.positionCount = 2;

            line.SetPosition(0, start);
            line.SetPosition(1, end);

            line.startWidth = tracerStartWidth;
            line.endWidth = tracerEndWidth;

            line.startColor =
                new Color(1f, 0.9f, 0.25f, 1f);

            line.endColor =
                new Color(1f, 0.2f, 0f, 0.15f);

            line.sharedMaterial = tracerMaterial != null
                ? tracerMaterial
                : runtimeTracerMaterial;

            yield return new WaitForSeconds(
                tracerDuration);

            Destroy(tracerObject);
        }

        private void EnsureReferences()
        {
            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }

            if (cameraViewController == null &&
                gameplayCamera != null)
            {
                cameraViewController =
                    gameplayCamera.GetComponent
                        <TopDownCameraFollow>();
            }
        }

        private void ValidateReferences()
        {
            if (firePoint == null)
            {
                Debug.LogError(
                    $"{nameof(TopDownPlayerController)} trên " +
                    $"{name} chưa được gán Fire Point.",
                    this);
            }

            if (weaponAimPivot == null)
            {
                Debug.LogError(
                    $"{nameof(TopDownPlayerController)} trên " +
                    $"{name} chưa được gán Weapon Aim Pivot.",
                    this);
            }

            if (gameplayCamera == null)
            {
                Debug.LogError(
                    "Không tìm thấy Main Camera.",
                    this);
            }
        }

        private void CreateRuntimeTracerMaterial()
        {
            if (tracerMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find(
                "Sprites/Default");

            if (shader == null)
            {
                return;
            }

            runtimeTracerMaterial =
                new Material(shader)
                {
                    name = "Runtime Tracer Material"
                };
        }

        private void OnDestroy()
        {
            if (runtimeTracerMaterial != null)
            {
                Destroy(runtimeTracerMaterial);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (firePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(
                    firePoint.position,
                    0.035f);

                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(
                    firePoint.position,
                    firePoint.forward * 1f);
            }

            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(
                    currentAimPoint,
                    0.08f);

                if (firePoint != null)
                {
                    Gizmos.DrawLine(
                        firePoint.position,
                        currentAimPoint);
                }
            }
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            fireRate = Mathf.Max(0.01f, fireRate);
            weaponRange = Mathf.Max(0f, weaponRange);
            weaponAimSpeed = Mathf.Max(0f, weaponAimSpeed);
            minimumAimDistance =
                Mathf.Max(0.1f, minimumAimDistance);

            tracerDuration =
                Mathf.Max(0f, tracerDuration);

            tracerStartWidth =
                Mathf.Max(0f, tracerStartWidth);

            tracerEndWidth =
                Mathf.Max(0f, tracerEndWidth);

            firstPersonTracerOffset =
                Mathf.Max(0f, firstPersonTracerOffset);

            thirdPersonTracerOffset =
                Mathf.Max(0f, thirdPersonTracerOffset);
        }
    }
}