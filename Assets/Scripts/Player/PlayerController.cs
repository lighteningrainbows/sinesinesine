
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("プレイヤーデータ")]
    [SerializeField] private PlayerData playerData;

    [Header("カメラ")]
    [SerializeField] private Transform cameraTransform;

    [Header("ジャンプ")]
    [SerializeField] private float jumpForce = 5.0f;

    [Header("カメラ感度")]
    [SerializeField] private float cameraSensitivity = 120f;

    [Header("カメラ上下制限")]
    [SerializeField] private float minCameraAngle = -80f;
    [SerializeField] private float maxCameraAngle = 80f;

    private Rigidbody rb;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private bool runInput;
    private bool jumpInput;

    private float cameraPitch;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (cameraTransform != null)
        {
            cameraPitch = cameraTransform.localEulerAngles.x;

            if (cameraPitch > 180f)
                cameraPitch -= 360f;
        }
    }

    private void Update()
    {
        ReadInput();
        RotateCamera();
    }

    private void FixedUpdate()
    {
        Move();

        if (jumpInput)
        {
            Jump();
            jumpInput = false;
        }
    }

    private void ReadInput()
    {
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        runInput = false;

        Gamepad pad = Gamepad.current;

        if (pad != null)
        {
            // 左スティック：移動
            moveInput = pad.leftStick.ReadValue();

            // 右スティック：カメラ
            lookInput = pad.rightStick.ReadValue();

            // B：ダッシュ
            runInput = pad.buttonEast.isPressed;

            // A：ジャンプ（押した瞬間）
            if (pad.buttonSouth.wasPressedThisFrame)
                jumpInput = true;
        }


        // キーボード移動（WASD / 矢印キー）
        if (Keyboard.current != null)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current.aKey.isPressed)
                horizontal -= 1f;

            if (Keyboard.current.dKey.isPressed)
                horizontal += 1f;

            if (Keyboard.current.sKey.isPressed)
                vertical -= 1f;

            if (Keyboard.current.wKey.isPressed)
                vertical += 1f;

            Vector2 keyboardMove =
                new Vector2(horizontal, vertical);

            if (keyboardMove.sqrMagnitude > 0f)
                moveInput = Vector2.ClampMagnitude(keyboardMove, 1f);
        }

        // マウス視点操作
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            lookInput += mouseDelta * 0.02f;
        }

        // 左Shift：ダッシュ
        if (Keyboard.current != null &&
            Keyboard.current.leftShiftKey.isPressed)
        {
            runInput = true;
        }

        // Space：ジャンプ
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpInput = true;
        }
    }

    private void Move()
    {
        if (playerData == null)
            return;

        float speed = playerData.MoveSpeed;

        if (runInput)
            speed *= 1.5f;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 direction =
            forward * moveInput.y +
            right * moveInput.x;

        direction = Vector3.ClampMagnitude(direction, 1f);

        Vector3 velocity = rb.linearVelocity;

        velocity.x = direction.x * speed;
        velocity.z = direction.z * speed;

        rb.linearVelocity = velocity;
    }

    private void Jump()
    {
        if (!IsGrounded())
            return;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(
            transform.position,
            Vector3.down,
            1.1f
        );
    }

    private void RotateCamera()
    {
        if (cameraTransform == null)
            return;

        float mouseOrStickX = lookInput.x;
        float mouseOrStickY = lookInput.y;

        // プレイヤー本体を左右に回転
        transform.Rotate(
            Vector3.up,
            mouseOrStickX * cameraSensitivity * Time.deltaTime
        );

        // カメラを上下に回転
        cameraPitch -=
            mouseOrStickY * cameraSensitivity * Time.deltaTime;

        cameraPitch = Mathf.Clamp(
            cameraPitch,
            minCameraAngle,
            maxCameraAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }
}