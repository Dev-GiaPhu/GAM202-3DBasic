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

        [Tooltip("Hệ số tốc độ khi giữ Shift.")]
        [SerializeField, Min(1f)]
        private float sprintMultiplier = 1.65f;

        [Tooltip("Độ cao nhảy khi nhấn Space.")]
        [SerializeField, Min(0f)]
        private float jumpHeight = 1.5f;

        [SerializeField]
        private float gravity = -25f;

        [Header("Weapon References")]
        [Tooltip("Điểm đặt ngay ngoài đầu nòng súng.")]
        [SerializeField]
        private Transform firePoint;

        [Header("Weapon Settings")]
        [SerializeField, Min(0.01f)]
        private float fireRate = 8f;

        [SerializeField, Min(0f)]
        private float weaponRange = 60f;

        [SerializeField, Min(1)]
        private int damagePerShot = 1;

        [SerializeField]
        private LayerMask aimMask = ~0;

        [Tooltip("Radius in which other zombies hear a shot and start chasing.")]
        [SerializeField, Min(0f)]
        private float gunshotAlertRadius = 18f;

        [Header("Aim")]
        [Tooltip("Khoảng cách tối thiểu của điểm ngắm.")]
        [SerializeField, Min(0.1f)]
        private float minimumAimDistance = 2f;

        [Header("Upper Body Aim")]
        [Tooltip("Khớp thân trên (thường là Spine hoặc Chest) sẽ cúi/ngửa theo tâm ngắm.")]
        [SerializeField]
        private Transform upperBodyAimJoint;

        [Tooltip("Đảo chiều nếu model cúi xuống khi tâm ngắm hướng lên.")]
        [SerializeField]
        private bool invertUpperBodyPitch = true;

        [SerializeField, Range(-89f, 0f)]
        private float minimumUpperBodyPitch = -35f;

        [SerializeField, Range(0f, 89f)]
        private float maximumUpperBodyPitch = 45f;

        [Tooltip("Góc bù của lưng so với tâm ngắm. Số dương ngửa thêm, số âm cúi thêm.")]
        [SerializeField, Range(-89f, 89f)]
        private float upperBodyPitchOffset;

        [Header("Upper Body Lean")]
        [Tooltip("Góc nghiêng tối đa khi giữ Q hoặc E.")]
        [SerializeField, Range(0f, 45f)]
        private float maximumUpperBodyLean = 15f;

        [SerializeField, Min(0f)]
        private float upperBodyLeanSpeed = 10f;

        [Header("Camera")]
        [Tooltip("Main Camera có Cinemachine Brain.")]
        [SerializeField]
        private Camera gameplayCamera;

        [Header("Tracer")]
        [SerializeField]
        private Material tracerMaterial;

        [SerializeField, Min(0f)]
        private float tracerDuration = 0.06f;

        [SerializeField, Min(0f)]
        private float tracerStartWidth = 0.035f;

        [SerializeField, Min(0f)]
        private float tracerEndWidth = 0.01f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
        private readonly Collider[] gunshotBuffer = new Collider[96];

        private CharacterController controller;
        private Material runtimeTracerMaterial;
        private PlayerSurvivalStats survivalStats;
        private WeaponAmmoInventory ammoInventory;
        private GameMenuController gameMenu;

        private Vector3 currentAimPoint;
        private float nextShotTime;
        private float currentUpperBodyPitch;
        private float currentUpperBodyLean;
        private Quaternion upperBodyBaseRotationRelativeToPlayer;
        private bool hasUpperBodyBaseRotation;
        private float verticalVelocity;

        public float UpperBodyLeanAngle => currentUpperBodyLean;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            survivalStats = GetComponent<PlayerSurvivalStats>() ??
                gameObject.AddComponent<PlayerSurvivalStats>();
            ammoInventory = GetComponent<WeaponAmmoInventory>() ??
                gameObject.AddComponent<WeaponAmmoInventory>();
            gameMenu = GetComponent<GameMenuController>() ??
                gameObject.AddComponent<GameMenuController>();

            if (GetComponent<SurvivalHUD>() == null)
            {
                gameObject.AddComponent<SurvivalHUD>();
            }

            if (GetComponent<PlayerDamageFeedback>() == null)
            {
                gameObject.AddComponent<PlayerDamageFeedback>();
            }

            EnsureReferences();
            CreateRuntimeTracerMaterial();
        }

        private void Start()
        {
            ValidateReferences();
        }

        private void Update()
        {
            if (gameMenu != null && gameMenu.GameplayBlocked)
            {
                survivalStats?.SetSprinting(false);
                return;
            }

            if (Keyboard.current == null)
            {
                return;
            }

            EnsureReferences();

            Move();
            UpdateAimPoint();

            if (Mouse.current == null ||
                Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            if (Mouse.current.leftButton.isPressed &&
                Time.time >= nextShotTime)
            {
                if (ammoInventory.TryConsumeRound())
                {
                    nextShotTime =
                        Time.time + 1f / Mathf.Max(0.01f, fireRate);

                    Fire();
                }
                else
                {
                    ammoInventory.TryStartReload();
                }
            }
        }

        private void LateUpdate()
        {
            UpdateUpperBodyAim();
        }

        public void Configure(Transform firePointTransform)
        {
            firePoint = firePointTransform;
        }

        public void ConfigureUpperBodyAim(Transform aimJoint)
        {
            upperBodyAimJoint = aimJoint;
            hasUpperBodyBaseRotation = false;
            currentUpperBodyPitch = 0f;
            currentUpperBodyLean = 0f;
        }

        private void UpdateUpperBodyAim()
        {
            if (upperBodyAimJoint == null)
            {
                return;
            }

            if (!hasUpperBodyBaseRotation)
            {
                upperBodyBaseRotationRelativeToPlayer =
                    Quaternion.Inverse(transform.rotation) *
                    upperBodyAimJoint.rotation;

                hasUpperBodyBaseRotation = true;
            }

            // Dùng trực tiếp hướng nhìn của camera cho pose lưng. Không dùng
            // currentAimPoint vì raycast có thể nhảy giữa mục tiêu, mặt đất và
            // điểm cuối tầm bắn, làm góc pitch đột ngột quay về 0.
            Vector3 aimDirection = gameplayCamera != null
                ? gameplayCamera.transform.forward
                : currentAimPoint - upperBodyAimJoint.position;

            if (aimDirection.sqrMagnitude < 0.001f)
            {
                return;
            }

            Vector3 localAimDirection =
                transform.InverseTransformDirection(
                    aimDirection.normalized);

            float horizontalDistance = new Vector2(
                localAimDirection.x,
                localAimDirection.z).magnitude;

            float targetPitch = Mathf.Atan2(
                localAimDirection.y,
                Mathf.Max(0.0001f, horizontalDistance)) *
                Mathf.Rad2Deg;

            targetPitch = Mathf.Clamp(
                targetPitch + upperBodyPitchOffset,
                minimumUpperBodyPitch,
                maximumUpperBodyPitch);

            currentUpperBodyPitch = targetPitch;

            float appliedPitch = invertUpperBodyPitch
                ? -currentUpperBodyPitch
                : currentUpperBodyPitch;

            float leanInput = 0f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.isPressed)
                {
                    leanInput += 1f;
                }

                if (Keyboard.current.eKey.isPressed)
                {
                    leanInput -= 1f;
                }
            }

            float targetLean = leanInput * maximumUpperBodyLean;

            if (upperBodyLeanSpeed <= 0f)
            {
                currentUpperBodyLean = targetLean;
            }
            else
            {
                float leanInterpolation = 1f - Mathf.Exp(
                    -upperBodyLeanSpeed * Time.deltaTime);

                currentUpperBodyLean = Mathf.Lerp(
                    currentUpperBodyLean,
                    targetLean,
                    leanInterpolation);
            }

            Quaternion pitchOffset = Quaternion.AngleAxis(
                appliedPitch,
                transform.right);

            Quaternion leanOffset = Quaternion.AngleAxis(
                currentUpperBodyLean,
                transform.forward);

            Quaternion baseRotation =
                transform.rotation *
                upperBodyBaseRotationRelativeToPlayer;

            // Gán rotation tuyệt đối từ pose gốc. Không nhân với rotation của
            // frame trước, vì vậy giữ tâm ở đâu thì lưng đứng yên tại góc đó.
            upperBodyAimJoint.rotation =
                leanOffset * pitchOffset * baseRotation;
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

            bool wantsToSprint =
                Keyboard.current.leftShiftKey.isPressed ||
                Keyboard.current.rightShiftKey.isPressed;

            wantsToSprint = wantsToSprint &&
                moveDirection.sqrMagnitude > 0.001f &&
                survivalStats.CanSprint;

            survivalStats.SetSprinting(wantsToSprint);

            float currentMoveSpeed = wantsToSprint
                ? moveSpeed * sprintMultiplier
                : moveSpeed;

            bool isGrounded = controller.isGrounded;

            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (isGrounded &&
                Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = Mathf.Sqrt(
                    jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity =
                moveDirection * currentMoveSpeed;

            velocity.y = verticalVelocity;

            CollisionFlags collisionFlags = controller.Move(
                velocity * Time.deltaTime);

            if ((collisionFlags & CollisionFlags.Below) != 0 &&
                verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
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
                    zombie.TakeDamage(
                        damagePerShot,
                        transform,
                        IsHeadshot(weaponHit.collider));
                }
            }

            AlertNearbyZombies(shotOrigin);

            StartCoroutine(
                DrawTracer(shotOrigin, shotEnd));
        }

        private void AlertNearbyZombies(Vector3 shotOrigin)
        {
            int count = Physics.OverlapSphereNonAlloc(
                shotOrigin,
                gunshotAlertRadius,
                gunshotBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                gunshotBuffer[i]
                    .GetComponentInParent<ZombieChaseReturn>()
                    ?.AlertByGunshot(transform);
            }
        }

        private static bool IsHeadshot(Collider hitCollider)
        {
            ZombieHitZone zone = hitCollider.GetComponent<ZombieHitZone>();
            if (zone != null)
            {
                return zone.IsHead;
            }

            Transform current = hitCollider.transform;
            while (current != null)
            {
                string lowerName = current.name.ToLowerInvariant();
                if (lowerName.Contains("head") || lowerName.Contains("skull"))
                {
                    return true;
                }

                if (current.GetComponent<ZombieChaseReturn>() != null)
                {
                    break;
                }

                current = current.parent;
            }

            return false;
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

        private void OnDisable()
        {
            if (upperBodyAimJoint != null &&
                hasUpperBodyBaseRotation)
            {
                upperBodyAimJoint.rotation =
                    transform.rotation *
                    upperBodyBaseRotationRelativeToPlayer;
            }

            currentUpperBodyPitch = 0f;
            currentUpperBodyLean = 0f;
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
            sprintMultiplier = Mathf.Max(1f, sprintMultiplier);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            gravity = Mathf.Min(-0.01f, gravity);
            fireRate = Mathf.Max(0.01f, fireRate);
            weaponRange = Mathf.Max(0f, weaponRange);
            damagePerShot = Mathf.Max(1, damagePerShot);
            minimumAimDistance =
                Mathf.Max(0.1f, minimumAimDistance);

            upperBodyLeanSpeed =
                Mathf.Max(0f, upperBodyLeanSpeed);

            maximumUpperBodyLean = Mathf.Clamp(
                maximumUpperBodyLean, 0f, 45f);

            minimumUpperBodyPitch = Mathf.Clamp(
                minimumUpperBodyPitch, -89f, 0f);

            maximumUpperBodyPitch = Mathf.Clamp(
                maximumUpperBodyPitch, 0f, 89f);

            tracerDuration =
                Mathf.Max(0f, tracerDuration);

            tracerStartWidth =
                Mathf.Max(0f, tracerStartWidth);

            tracerEndWidth =
                Mathf.Max(0f, tracerEndWidth);

        }
    }
}
