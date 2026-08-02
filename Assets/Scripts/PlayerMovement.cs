using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float runSpeed = 8f;
    public float turnSpeed = 10f;

    [Header("Gravity")]
    public float gravity = -20f;

    [Header("Jump")]
    public float timeJumpWait = 1f;

    public CharacterController characterController;
    public Animator animator;

    public InputAction moveAction;
    public InputAction jumpAction;
    public InputAction runAction;

    private Vector3 move = Vector3.zero;
    private float verticalVelocity;

    private bool canJump = true;
    private float jumpTimer = 0f;

    void Start()
    {
        moveAction.Enable();
        jumpAction.Enable();
        runAction.Enable();
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        runAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        runAction.Disable();
    }

    void Update()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        bool isRunning = runAction.IsPressed();
        float currentSpeed = isRunning ? runSpeed : speed;

        // Hướng theo camera
        Transform cam = Camera.main.transform;

        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;

        camForward.y = 0f;
        camRight.y = 0f;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDirection = camForward * input.y + camRight * input.x;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            moveDirection.Normalize();

            move.x = moveDirection.x * currentSpeed;
            move.z = moveDirection.z * currentSpeed;

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime);
        }
        else
        {
            move.x = 0f;
            move.z = 0f;
        }

        // Gravity
        if (characterController.isGrounded)
        {
            // Giữ nhân vật dính mặt đất
            if (verticalVelocity < 0)
                verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        move.y = verticalVelocity;

        characterController.Move(move * Time.deltaTime);

        // Cooldown nhảy
        if (!canJump)
        {
            jumpTimer += Time.deltaTime;

            if (jumpTimer >= timeJumpWait)
            {
                canJump = true;
                jumpTimer = 0f;
            }
        }

        // Chỉ phát animation nhảy
        if (jumpAction.WasPressedThisFrame() && canJump && characterController.isGrounded)
        {
            canJump = false;
            animator.SetTrigger("Jump");
        }

        animator.SetFloat("Speed", input.magnitude);
        animator.SetBool("IsRunning", isRunning);
    }
}