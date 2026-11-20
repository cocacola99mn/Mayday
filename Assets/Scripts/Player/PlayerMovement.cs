using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Resident Evil 7 style first-person body: heavy acceleration, slower backpedal
/// and strafe, forward-only sprint, and a toggle crouch that will not stand into a ceiling.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Player/Move on InputSystem_Actions.")]
    InputActionReference moveAction;

    [SerializeField, Tooltip("Player/Sprint on InputSystem_Actions. Hold to run. Only works while moving forward.")]
    InputActionReference sprintAction;

    [SerializeField, Tooltip("Player/Crouch on InputSystem_Actions. Press to toggle.")]
    InputActionReference crouchAction;

    [SerializeField, Tooltip("CameraRoot. Its local height eases down while crouched.")]
    Transform cameraRoot;

    [Header("Speed")]
    [SerializeField] float walkSpeed = 2f;
    [SerializeField] float sprintSpeed = 3.6f;
    [SerializeField] float crouchSpeed = 1.1f;
    [SerializeField] float backwardMultiplier = 0.7f;
    [SerializeField] float strafeMultiplier = 0.85f;
    [SerializeField] float acceleration = 10f;
    [SerializeField] float deceleration = 12f;

    [Header("Gravity")]
    [SerializeField] float gravity = -20f;
    [SerializeField] float groundedStick = -2f;

    [Header("Crouch")]
    [SerializeField] float standingHeight = 1.7f;
    [SerializeField] float crouchHeight = 1f;
    [SerializeField] float standingCameraHeight = 1.6f;
    [SerializeField] float crouchCameraHeight = 0.9f;
    [SerializeField] float crouchTransition = 0.25f;
    [SerializeField, Tooltip("Geometry that blocks standing up. The Player layer is always ignored.")]
    LayerMask ceilingMask = Physics.DefaultRaycastLayers;

    CharacterController controller;
    PlayerLook look;
    Vector3 planarVelocity;
    float verticalVelocity;
    float crouchBlend;
    bool crouching;
    bool sprinting;

    public float HorizontalSpeed => planarVelocity.magnitude;
    public float StepDistanceThisFrame { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsSprinting => sprinting;
    public bool IsCrouching => crouching;
    public bool IsQuickTurning => look != null && look.IsQuickTurning;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        look = GetComponent<PlayerLook>();
        verticalVelocity = groundedStick;
        ApplyBodyHeight();
    }

    void OnEnable()
    {
        Enable(moveAction);
        Enable(sprintAction);
        Enable(crouchAction);
    }

    void OnDisable()
    {
        Disable(crouchAction);
        if (look == null || !look.isActiveAndEnabled)
        {
            Disable(moveAction);
            Disable(sprintAction);
        }
    }

    void Update()
    {
        Vector2 move = ReadMove();
        if (WasPressed(crouchAction))
            ToggleCrouch();

        bool quickTurning = IsQuickTurning;
        bool holdingSprint = IsPressed(sprintAction);
        sprinting = !crouching
            && !quickTurning
            && holdingSprint
            && move.y > 0.1f
            && move.y >= Mathf.Abs(move.x);

        float speed = crouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;
        Vector3 wish = transform.right * move.x + transform.forward * move.y;
        float inputAmount = Mathf.Clamp01(wish.magnitude);
        if (inputAmount > 0.0001f)
            wish /= wish.magnitude;
        if (quickTurning)
            inputAmount = 0f;

        Vector3 target = wish * (inputAmount * speed * DirectionMultiplier(move));
        float rate = target.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;
        planarVelocity = Vector3.MoveTowards(planarVelocity, target, rate * Time.deltaTime);
        planarVelocity.y = 0f;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedStick;
        else
            verticalVelocity += gravity * Time.deltaTime;

        Vector3 before = transform.position;
        Vector3 displacement = planarVelocity;
        displacement.y = verticalVelocity;
        controller.Move(displacement * Time.deltaTime);

        Vector3 travelled = transform.position - before;
        travelled.y = 0f;
        StepDistanceThisFrame = travelled.magnitude;
        IsGrounded = controller.isGrounded;

        float expected = planarVelocity.magnitude * Time.deltaTime;
        if (expected > 0.0001f && StepDistanceThisFrame < expected * 0.5f)
            planarVelocity *= StepDistanceThisFrame / expected;

        UpdateCrouch();
    }

    void ToggleCrouch()
    {
        if (!crouching)
        {
            crouching = true;
            return;
        }

        if (!CeilingBlocksStand())
            crouching = false;
    }

    void UpdateCrouch()
    {
        if (!crouching && crouchBlend > 0f && CeilingBlocksStand())
            crouching = true;

        float target = crouching ? 1f : 0f;
        float step = crouchTransition <= 0f ? 1f : Time.deltaTime / crouchTransition;
        crouchBlend = Mathf.MoveTowards(crouchBlend, target, step);
        ApplyBodyHeight();
    }

    void ApplyBodyHeight()
    {
        float height = Mathf.Lerp(standingHeight, crouchHeight, crouchBlend);
        controller.height = height;
        controller.center = new Vector3(0f, height * 0.5f, 0f);

        if (cameraRoot == null)
            return;

        Vector3 cameraPosition = cameraRoot.localPosition;
        cameraPosition.y = Mathf.Lerp(standingCameraHeight, crouchCameraHeight, crouchBlend);
        cameraRoot.localPosition = cameraPosition;
    }

    bool CeilingBlocksStand()
    {
        float radius = controller.radius * 0.95f;
        Vector3 bottom = transform.position + Vector3.up * (radius + 0.05f);
        Vector3 top = transform.position + Vector3.up * (standingHeight - radius - 0.02f);
        if (top.y <= bottom.y)
            return false;

        int mask = ceilingMask.value & ~(1 << gameObject.layer);
        return Physics.CheckCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
    }

    float DirectionMultiplier(Vector2 move)
    {
        float magnitude = move.magnitude;
        if (magnitude < 0.01f)
            return 1f;

        Vector2 direction = move / magnitude;
        float forward = Mathf.Max(0f, direction.y);
        float backward = Mathf.Max(0f, -direction.y);
        float strafe = Mathf.Abs(direction.x);
        float weight = forward + backward + strafe;
        if (weight < 0.01f)
            return 1f;

        return (forward + backward * backwardMultiplier + strafe * strafeMultiplier) / weight;
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
