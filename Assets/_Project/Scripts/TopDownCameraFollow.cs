using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieInfinite
{
    public sealed class TopDownCameraFollow : MonoBehaviour
    {
        [Header("Player Target")]
        [SerializeField] private Transform target;

        [Header("Cinemachine Cameras")]
        [SerializeField] private CinemachineCamera firstPersonCamera;
        [SerializeField] private CinemachineCamera thirdPersonCamera;

        [Header("Camera Rig")]
        [SerializeField, Min(0f)] private float eyeHeight = 1.65f;

        [SerializeField]
        private Vector3 firstPersonOffset = new(0f, 0f, 0.08f);

        [SerializeField]
        private Vector3 thirdPersonOffset = new(0.65f, 0.25f, -4.2f);

        [Header("Head Follow")]
        [Tooltip("Khớp Head của model. Để trống sẽ dùng Eye Height trên Player.")]
        [SerializeField]
        private Transform headTarget;

        [Tooltip("Vị trí camera tương đối với khớp Head.")]
        [SerializeField]
        private Vector3 headPositionOffset;

        [Tooltip("Tốc độ camera đi theo chuyển động của đầu. Đặt 0 để bám ngay lập tức.")]
        [SerializeField, Min(0f)]
        private float headFollowSpeed = 25f;

        [Tooltip("Tỷ lệ góc roll camera so với góc nghiêng Q/E của cơ thể.")]
        [SerializeField, Range(0f, 2f)]
        private float leanRollMultiplier = 1f;

        [Header("Mouse Look")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;

        [SerializeField]
        private Vector2 pitchLimits = new(-70f, 80f);

        [Header("View")]
        [SerializeField] private bool startInFirstPerson;
        [SerializeField] private bool lockCursorOnStart = true;

        [Header("Damage Shake")]
        [SerializeField, Min(0.01f)] private float damageShakeDuration = 0.28f;
        [SerializeField, Min(0f)] private float damageShakePosition = 0.12f;
        [SerializeField, Min(0f)] private float damageShakeRotation = 2.2f;

        private Transform cameraRoot;
        private Transform pitchPivot;
        private Transform firstPersonAnchor;
        private Transform thirdPersonAnchor;
        private TopDownPlayerController playerController;
        private GameMenuController gameMenu;

        private float yaw;
        private float pitch;
        private bool initialized;
        private float shakeRemaining;
        private float shakeIntensity;
        private Vector3 shakeEuler;

        public bool IsFirstPerson { get; private set; }

        public Transform AimTransform
        {
            get
            {
                if (pitchPivot != null)
                {
                    return pitchPivot;
                }

                return target;
            }
        }

        private void Start()
        {
            if (target != null)
            {
                BuildCameraRig();
            }

            if (lockCursorOnStart &&
                (gameMenu == null || !gameMenu.GameplayBlocked))
            {
                LockCursor();
            }
        }

        private void Update()
        {
            if (!initialized)
            {
                if (target != null)
                {
                    BuildCameraRig();
                }

                return;
            }

            if (Keyboard.current != null &&
                Keyboard.current.vKey.wasPressedThisFrame)
            {
                SetFirstPerson(!IsFirstPerson);
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                HandleMouseLook();
            }
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            if (gameMenu != null && gameMenu.GameplayBlocked)
            {
                return;
            }

            UpdateHeadFollowAndLean();
        }

        /// <summary>
        /// Được gọi khi player được tạo bằng code.
        /// </summary>
        public void Configure(Transform followTarget)
        {
            if (followTarget == null)
            {
                Debug.LogError(
                    $"{nameof(TopDownCameraFollow)} received a null target.",
                    this);

                return;
            }

            target = followTarget;
            BuildCameraRig();
        }

        public void SetFirstPerson(bool firstPerson)
        {
            if (!initialized ||
                firstPersonCamera == null ||
                thirdPersonCamera == null)
            {
                return;
            }

            IsFirstPerson = firstPerson;

            if (firstPerson)
            {
                firstPersonCamera.enabled = true;
                thirdPersonCamera.enabled = false;
            }
            else
            {
                thirdPersonCamera.enabled = true;
                firstPersonCamera.enabled = false;
            }
        }

        public void ToggleView()
        {
            SetFirstPerson(!IsFirstPerson);
        }

        private void BuildCameraRig()
        {
            if (target == null)
            {
                return;
            }

            gameMenu = target.GetComponent<GameMenuController>() ??
                target.GetComponentInParent<GameMenuController>();

            if (firstPersonCamera == null || thirdPersonCamera == null)
            {
                Debug.LogError(
                    "Chưa gán First Person Camera hoặc Third Person Camera.",
                    this);

                initialized = false;
                return;
            }

            cameraRoot = GetOrCreateChild(target, "CameraRigRoot");
            cameraRoot.localPosition = new Vector3(0f, eyeHeight, 0f);
            cameraRoot.localRotation = Quaternion.identity;
            cameraRoot.localScale = Vector3.one;

            pitchPivot = GetOrCreateChild(cameraRoot, "CameraPitchPivot");
            pitchPivot.localPosition = Vector3.zero;
            pitchPivot.localRotation = Quaternion.identity;
            pitchPivot.localScale = Vector3.one;

            firstPersonAnchor =
                GetOrCreateChild(pitchPivot, "FirstPersonCameraAnchor");

            firstPersonAnchor.localPosition = firstPersonOffset;
            firstPersonAnchor.localRotation = Quaternion.identity;
            firstPersonAnchor.localScale = Vector3.one;

            thirdPersonAnchor =
                GetOrCreateChild(pitchPivot, "ThirdPersonCameraAnchor");

            thirdPersonAnchor.localPosition = thirdPersonOffset;
            thirdPersonAnchor.localRotation = Quaternion.identity;
            thirdPersonAnchor.localScale = Vector3.one;

            playerController = target.GetComponent<TopDownPlayerController>();

            AttachCamera(firstPersonCamera, firstPersonAnchor);
            AttachCamera(thirdPersonCamera, thirdPersonAnchor);

            yaw = target.eulerAngles.y;
            pitch = 0f;

            initialized = true;
            SetFirstPerson(startInFirstPerson);
        }

        private void HandleMouseLook()
        {
            if (Mouse.current == null || target == null)
            {
                return;
            }

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            yaw += mouseDelta.x * mouseSensitivity;
            pitch -= mouseDelta.y * mouseSensitivity;

            pitch = Mathf.Clamp(
                pitch,
                pitchLimits.x,
                pitchLimits.y);

            // Player xoay theo chiều ngang của camera.
            target.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Chỉ camera xoay lên xuống.
            UpdateViewRotation();
        }

        public void TriggerDamageShake(float intensity = 1f)
        {
            shakeRemaining = damageShakeDuration;
            shakeIntensity = Mathf.Max(shakeIntensity, Mathf.Max(0f, intensity));
        }

        private void UpdateHeadFollowAndLean()
        {
            Vector3 targetPosition = headTarget != null
                ? headTarget.TransformPoint(headPositionOffset)
                : target.TransformPoint(new Vector3(0f, eyeHeight, 0f));

            if (headFollowSpeed <= 0f)
            {
                cameraRoot.position = targetPosition;
            }
            else
            {
                float interpolation = 1f - Mathf.Exp(
                    -headFollowSpeed * Time.deltaTime);

                cameraRoot.position = Vector3.Lerp(
                    cameraRoot.position,
                    targetPosition,
                    interpolation);
            }

            UpdateDamageShake();

            UpdateViewRotation();
        }

        private void UpdateDamageShake()
        {
            if (shakeRemaining <= 0f)
            {
                shakeEuler = Vector3.zero;
                shakeIntensity = 0f;
                return;
            }

            shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.unscaledDeltaTime);
            float fade = Mathf.Clamp01(shakeRemaining / damageShakeDuration);
            float strength = shakeIntensity * fade;
            cameraRoot.position += Random.insideUnitSphere * (damageShakePosition * strength);
            shakeEuler = Random.insideUnitSphere * (damageShakeRotation * strength);
        }

        private void UpdateViewRotation()
        {
            if (pitchPivot == null)
            {
                return;
            }

            float leanAngle = playerController != null
                ? playerController.UpperBodyLeanAngle * leanRollMultiplier
                : 0f;

            // Cả FPS và TPS đều nằm dưới pivot này nên cùng nhận pitch và roll.
            pitchPivot.localRotation = Quaternion.Euler(
                pitch + shakeEuler.x,
                shakeEuler.y,
                leanAngle + shakeEuler.z);
        }

        private static void AttachCamera(
            CinemachineCamera cinemachineCamera,
            Transform anchor)
        {
            Transform cameraTransform = cinemachineCamera.transform;

            cameraTransform.SetParent(anchor, false);
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
            cameraTransform.localScale = Vector3.one;
        }

        private static Transform GetOrCreateChild(
            Transform parent,
            string childName)
        {
            Transform child = parent.Find(childName);

            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);

            return childObject.transform;
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnValidate()
        {
            eyeHeight = Mathf.Max(0f, eyeHeight);
            mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
            headFollowSpeed = Mathf.Max(0f, headFollowSpeed);
            leanRollMultiplier = Mathf.Clamp(
                leanRollMultiplier,
                0f,
                2f);

            if (pitchLimits.x > pitchLimits.y)
            {
                (pitchLimits.x, pitchLimits.y) =
                    (pitchLimits.y, pitchLimits.x);
            }
        }
    }
}
