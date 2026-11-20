using UnityEngine;

/// <summary>
/// Makes the gun lag behind the view when you turn and bob while you walk.
/// Sits between WeaponHolder and the gun model.
/// </summary>
[DisallowMultipleComponent]
public class WeaponSway : MonoBehaviour
{
    [Header("References")]
    [SerializeField] PlayerMovement movement;
    [SerializeField] PlayerWeapon weapon;

    [Header("Turn Lag")]
    [SerializeField, Tooltip("Degrees of tilt per degree-per-second of turning.")]
    float tiltPerTurnSpeed = 0.02f;
    [SerializeField] float maxTilt = 4f;
    [SerializeField, Tooltip("Metres of drift per degree of tilt.")]
    float driftPerTilt = 0.002f;
    [SerializeField] float smoothing = 10f;

    [Header("Walk Bob")]
    [SerializeField] float bobAmplitude = 0.006f;
    [SerializeField, Tooltip("Metres of travel for one full bob.")]
    float bobStepLength = 1.3f;

    [Header("Aiming")]
    [SerializeField, Range(0f, 1f), Tooltip("How much sway is left while the gun is raised.")]
    float aimMultiplier = 0.35f;

    Transform view;
    Quaternion previousViewRotation;
    Vector2 tilt;
    float bobPhase;
    float bobWeight;

    void Awake()
    {
        view = transform.parent;
        if (view != null)
            previousViewRotation = view.rotation;
    }

    void OnEnable()
    {
        if (view != null)
            previousViewRotation = view.rotation;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (view == null || dt <= 0f)
            return;

        // How fast the view turned this frame, in its own space.
        Vector3 turn = (Quaternion.Inverse(previousViewRotation) * view.rotation).eulerAngles;
        previousViewRotation = view.rotation;
        float yawSpeed = Mathf.DeltaAngle(0f, turn.y) / dt;
        float pitchSpeed = Mathf.DeltaAngle(0f, turn.x) / dt;

        Vector2 targetTilt = new Vector2(
            Mathf.Clamp(-pitchSpeed * tiltPerTurnSpeed, -maxTilt, maxTilt),
            Mathf.Clamp(-yawSpeed * tiltPerTurnSpeed, -maxTilt, maxTilt));
        tilt = Vector2.Lerp(tilt, targetTilt, 1f - Mathf.Exp(-smoothing * dt));

        bool walking = movement != null && movement.IsGrounded && movement.HorizontalSpeed > 0.25f;
        if (walking)
            bobPhase += movement.StepDistanceThisFrame / Mathf.Max(0.05f, bobStepLength) * Mathf.PI * 2f;
        bobWeight = Mathf.MoveTowards(bobWeight, walking ? 1f : 0f, dt * 4f);

        float scale = weapon != null ? Mathf.Lerp(1f, aimMultiplier, weapon.AimBlend) : 1f;
        Vector3 bob = new Vector3(Mathf.Cos(bobPhase * 0.5f), Mathf.Sin(bobPhase) * 0.6f, 0f) * (bobAmplitude * bobWeight);
        Vector3 drift = new Vector3(tilt.y, -tilt.x, 0f) * driftPerTilt;

        transform.SetLocalPositionAndRotation(
            (bob + drift) * scale,
            Quaternion.Euler(tilt.x * scale, tilt.y * scale, tilt.y * scale * 0.5f));
    }
}
