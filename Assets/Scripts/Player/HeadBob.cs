using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Distance-driven head bob on CameraBob. One footstep fires each time the head
/// reaches the bottom of a step, so audio can be hooked up from the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class HeadBob : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Leave empty to use the PlayerMovement on a parent.")]
    PlayerMovement movement;

    [Header("Bob")]
    [SerializeField] float walkAmplitude = 0.03f;
    [SerializeField] float sprintAmplitude = 0.05f;
    [SerializeField] float crouchAmplitude = 0.015f;
    [SerializeField] float lateralAmplitude = 0.012f;
    [SerializeField, Tooltip("Metres of travel for one footstep while walking.")]
    float walkStepLength = 1.3f;
    [SerializeField] float sprintStepLength = 1.7f;
    [SerializeField] float crouchStepLength = 0.85f;
    [SerializeField] float returnSpeed = 8f;
    [SerializeField] float minimumSpeed = 0.25f;

    [Header("Events")]
    [SerializeField] UnityEvent onFootstep;

    Vector3 restLocalPosition;
    Vector3 bobOffset;
    float phase;

    void Awake()
    {
        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();
        restLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        if (movement == null)
            return;

        bool stepping = movement.IsGrounded
            && !movement.IsQuickTurning
            && movement.HorizontalSpeed > minimumSpeed
            && movement.StepDistanceThisFrame > 0.0001f;

        if (stepping)
        {
            float stepLength = movement.IsCrouching
                ? crouchStepLength
                : movement.IsSprinting ? sprintStepLength : walkStepLength;
            stepLength = Mathf.Max(0.05f, stepLength);

            float previousPhase = phase;
            phase += movement.StepDistanceThisFrame / stepLength * Mathf.PI * 2f;

            float amplitude = movement.IsCrouching
                ? crouchAmplitude
                : movement.IsSprinting ? sprintAmplitude : walkAmplitude;
            float lateral = movement.IsCrouching ? lateralAmplitude * 0.5f : lateralAmplitude;
            bobOffset = new Vector3(Mathf.Cos(phase * 0.5f) * lateral, Mathf.Sin(phase) * amplitude, 0f);

            if (FootstepIndex(phase) != FootstepIndex(previousPhase))
                onFootstep?.Invoke();
        }
        else
        {
            bobOffset = Vector3.MoveTowards(bobOffset, Vector3.zero, returnSpeed * Time.deltaTime);
        }

        transform.localPosition = restLocalPosition + bobOffset;
    }

    static int FootstepIndex(float value)
    {
        return Mathf.FloorToInt((value - Mathf.PI * 1.5f) / (Mathf.PI * 2f));
    }
}
