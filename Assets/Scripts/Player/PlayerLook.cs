using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Yaw on the player body, pitch on CameraRoot, and the Resident Evil 7 quick turn
/// (hold back and press sprint to spin 180 degrees).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class PlayerLook : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Player/Look on InputSystem_Actions.")]
    InputActionReference lookAction;

    [SerializeField, Tooltip("Player/Move on InputSystem_Actions. Used to detect backing up for the quick turn.")]
    InputActionReference moveAction;

    [SerializeField, Tooltip("Player/Sprint on InputSystem_Actions. Press while backing up to quick turn.")]
    InputActionReference sprintAction;

    [SerializeField, Tooltip("CameraRoot. Pitch is applied here so the body stays upright.")]
    Transform cameraRoot;

    [Header("Look")]
    [SerializeField, Tooltip("Degrees per pixel. Mouse look is not scaled by frame time.")]
    float mouseSensitivity = 0.05f;

    [SerializeField, Tooltip("Degrees per second at full stick deflection.")]
    float stickLookSpeed = 80f;

    [SerializeField] float pitchLimit = 80f;
    [SerializeField, Tooltip("Seconds of catch-up after a look input. Zero is instant.")]
    float lookSmoothTime = 0.03f;

    [SerializeField] bool invertY;

    [Header("Quick Turn")]
    [SerializeField] float quickTurnDuration = 0.35f;
    [SerializeField] float backwardThreshold = 0.5f;

    PlayerMovement movement;
    float yaw;
    float pitch;
    float targetYaw;
    float targetPitch;
    float yawSmoothVelocity;
    float pitchSmoothVelocity;
    bool turning;
    float turnStartYaw;
    float turnElapsed;
    Vector2 previousMove;

    public bool IsQuickTurning => turning;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        yaw = targetYaw = transform.eulerAngles.y;
        if (cameraRoot == null)
            return;

        pitch = cameraRoot.localEulerAngles.x;
        if (pitch > 180f)
            pitch -= 360f;
        targetPitch = pitch;
    }

    void OnEnable()
    {
        Enable(lookAction);
        Enable(moveAction);
        Enable(sprintAction);
        LockCursor();
    }

    void OnDisable()
    {
        Disable(lookAction);
        if (movement == null || !movement.isActiveAndEnabled)
        {
            Disable(moveAction);
            Disable(sprintAction);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        UpdateCursor();

        if (turning)
            AdvanceQuickTurn();
        else if (!TryStartQuickTurn())
            ApplyLook();

        ApplyRotation();
        previousMove = ReadMove();
    }

    void ApplyLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;
        if (lookAction == null || lookAction.action == null)
            return;

        Vector2 look = lookAction.action.ReadValue<Vector2>();
        if (look.sqrMagnitude < 0.000001f)
            return;

        InputDevice device = lookAction.action.activeControl != null
            ? lookAction.action.activeControl.device
            : null;
        bool stick = device is Gamepad || device is Joystick;
        float scale = stick ? stickLookSpeed * Time.deltaTime : mouseSensitivity;
        float pitchSign = invertY ? 1f : -1f;

        targetYaw += look.x * scale;
        targetPitch = Mathf.Clamp(targetPitch + look.y * scale * pitchSign, -pitchLimit, pitchLimit);
    }

    bool TryStartQuickTurn()
    {
        if (movement != null && (!movement.IsGrounded || movement.IsCrouching))
            return false;

        Vector2 move = ReadMove();
        bool backingUp = move.y < -backwardThreshold && Mathf.Abs(move.x) <= backwardThreshold;
        if (!backingUp)
            return false;

        bool sprintPressed = WasPressed(sprintAction);
        bool justStartedBackingUp = previousMove.y >= -backwardThreshold;
        if (!sprintPressed && !(IsPressed(sprintAction) && justStartedBackingUp))
            return false;

        turning = true;
        turnElapsed = 0f;
        turnStartYaw = targetYaw;
        yawSmoothVelocity = 0f;
        return true;
    }

    void AdvanceQuickTurn()
    {
        turnElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, quickTurnDuration);
        float t = Mathf.Clamp01(turnElapsed / duration);
        float eased = t * t * (3f - 2f * t);
        targetYaw = turnStartYaw + 180f * eased;
        yaw = targetYaw;
        yawSmoothVelocity = 0f;

        if (t >= 1f)
            turning = false;
    }

    void ApplyRotation()
    {
        if (turning || lookSmoothTime <= 0f)
        {
            yaw = targetYaw;
            pitch = targetPitch;
        }
        else
        {
            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawSmoothVelocity, lookSmoothTime);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchSmoothVelocity, lookSmoothTime);
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraRoot != null)
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    Vector2 ReadMove()
    {
        if (moveAction == null || moveAction.action == null)
            return Vector2.zero;
        return moveAction.action.ReadValue<Vector2>();
    }

    static bool IsPressed(InputActionReference reference)
    {
        return reference != null && reference.action != null && reference.action.IsPressed();
    }

    static bool WasPressed(InputActionReference reference)
    {
        return reference != null && reference.action != null && reference.action.WasPressedThisFrame();
    }

    void UpdateCursor()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Cursor.lockState != CursorLockMode.Locked
            && Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    static void Enable(InputActionReference reference)
    {
        if (reference != null && reference.action != null)
            reference.action.Enable();
    }

    static void Disable(InputActionReference reference)
    {
        if (reference != null && reference.action != null)
            reference.action.Disable();
    }
}
