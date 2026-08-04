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

        [Header("Mouse Look")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;

        [SerializeField]
        private Vector2 pitchLimits = new(-70f, 80f);

        [Header("View")]
        [SerializeField] private bool startInFirstPerson;
        [SerializeField] private bool lockCursorOnStart = true;

        private Transform cameraRoot;
        private Transform pitchPivot;
        private Transform firstPersonAnchor;
        private Transform thirdPersonAnchor;

        private float yaw;
        private float pitch;
        private bool initialized;

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

            if (lockCursorOnStart)
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

            HandleCursor();

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
            pitchPivot.localRotation =
                Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleCursor()
        {
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                UnlockCursor();
                return;
            }

            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame &&
                Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }
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

            if (pitchLimits.x > pitchLimits.y)
            {
                (pitchLimits.x, pitchLimits.y) =
                    (pitchLimits.y, pitchLimits.x);
            }
        }
    }
}