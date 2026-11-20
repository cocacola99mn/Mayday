using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Resident Evil 7 style handgun: hold aim to raise it and tighten the spread, one shot
/// per press, magazine plus reserve ammo, and a manual reload. Sits on WeaponHolder and
/// moves it between the hip, aim, and lowered poses.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWeapon : MonoBehaviour
{
    enum State { Drawing, Ready, Reloading }

    static readonly int DrawState = Animator.StringToHash("Draw");
    static readonly int FireState = Animator.StringToHash("Fire");
    static readonly int ReloadState = Animator.StringToHash("Reload");
    static readonly int ReloadEmptyState = Animator.StringToHash("ReloadEmpty");

    [Header("References")]
    [SerializeField] WeaponData data;

    [SerializeField, Tooltip("Player/Attack on InputSystem_Actions.")]
    InputActionReference attackAction;

    [SerializeField, Tooltip("Player/Aim on InputSystem_Actions. Hold to raise the gun.")]
    InputActionReference aimAction;

    [SerializeField, Tooltip("Player/Reload on InputSystem_Actions.")]
    InputActionReference reloadAction;

    [SerializeField] PlayerMovement movement;
    [SerializeField] PlayerLook look;
    [SerializeField, Tooltip("Bullets leave from the centre of this camera.")]
    Camera viewCamera;
    [SerializeField] Animator animator;

    [Header("Effects")]
    [SerializeField] ParticleSystem muzzleFlash;
    [SerializeField] Light muzzleLight;
    [SerializeField] float muzzleLightTime = 0.05f;
    [SerializeField, Tooltip("World-space burst that is moved to each bullet hit.")]
    ParticleSystem impactEffect;
    [SerializeField] int impactParticles = 10;

    [Header("Audio (optional)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip fireClip;
    [SerializeField] AudioClip dryFireClip;
    [SerializeField] AudioClip reloadClip;

    [Header("Ammo")]
    [SerializeField] int startingReserve = 30;

    [Header("Poses (local to CameraBob)")]
    [SerializeField] Vector3 hipPosition;
    [SerializeField] Vector3 hipRotation;
    [SerializeField] Vector3 aimPosition;
    [SerializeField] Vector3 aimRotation;
    [SerializeField, Tooltip("Used while sprinting.")]
    Vector3 loweredPosition;
    [SerializeField] Vector3 loweredRotation;
    [SerializeField] float lowerTime = 0.2f;
    [SerializeField] float kickReturnSpeed = 14f;

    State state;
    float stateTime;
    float stateDuration;
    bool reloadAmmoMoved;
    int magazine;
    int reserve;
    float nextFireTime;
    float aimBlend;
    float lowerBlend;
    float bloom;
    float kickBack;
    float kickPitch;
    float baseFieldOfView;
    float muzzleLightOffTime;
    bool cursorWasLocked;

    public int AmmoInMagazine => magazine;
    public int ReserveAmmo => reserve;
    public int MagazineSize => data != null ? data.magazineSize : 0;
    public float AimBlend => aimBlend;
    public bool IsAiming => aimBlend > 0.5f;
    public bool IsReloading => state == State.Reloading;
    public float SpreadDegrees { get; private set; }
    public Camera ViewCamera => viewCamera;

    void Awake()
    {
        if (data == null)
        {
            Debug.LogError("PlayerWeapon has no WeaponData assigned.", this);
            enabled = false;
            return;
        }

        magazine = data.magazineSize;
        reserve = startingReserve;
        if (viewCamera != null)
            baseFieldOfView = viewCamera.fieldOfView;
        if (muzzleLight != null)
            muzzleLight.enabled = false;
    }

    void OnEnable()
    {
        Enable(attackAction);
        Enable(aimAction);
        Enable(reloadAction);
        BeginState(State.Drawing, data.drawTime);
        if (animator != null)
            animator.Play(DrawState, 0, 0f);
    }

    void OnDisable()
    {
        Disable(attackAction);
        Disable(aimAction);
        Disable(reloadAction);
        if (movement != null)
            movement.IsAiming = false;
        if (viewCamera != null && baseFieldOfView > 0f)
            viewCamera.fieldOfView = baseFieldOfView;
        if (muzzleLight != null)
            muzzleLight.enabled = false;
    }

    void Update()
    {
        // The click that re-locks the cursor must not also fire a shot.
        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
        bool hasControl = cursorLocked && cursorWasLocked;
        cursorWasLocked = cursorLocked;

        bool quickTurning = movement != null && movement.IsQuickTurning;
        bool wantsAim = hasControl && IsPressed(aimAction) && state != State.Drawing && !quickTurning;
        if (movement != null)
            movement.IsAiming = wantsAim;

        bool lowered = movement != null && movement.IsSprinting && !wantsAim && state != State.Reloading;
        float dt = Time.deltaTime;
        aimBlend = Mathf.MoveTowards(aimBlend, wantsAim ? 1f : 0f, dt / Mathf.Max(0.01f, data.aimTime));
        lowerBlend = Mathf.MoveTowards(lowerBlend, lowered ? 1f : 0f, dt / Mathf.Max(0.01f, lowerTime));

        AdvanceState(dt);

        if (hasControl && state == State.Ready)
        {
            if (WasPressed(reloadAction))
                TryReload();
            else if (WasPressed(attackAction))
                TryFire();
        }

        UpdateSpread(dt);

        float kickDecay = 1f - Mathf.Exp(-kickReturnSpeed * dt);
        kickBack = Mathf.Lerp(kickBack, 0f, kickDecay);
        kickPitch = Mathf.Lerp(kickPitch, 0f, kickDecay);

        if (viewCamera != null)
            viewCamera.fieldOfView = Mathf.Lerp(baseFieldOfView, data.aimFieldOfView, Ease(aimBlend));

        if (muzzleLight != null && muzzleLight.enabled && Time.time >= muzzleLightOffTime)
            muzzleLight.enabled = false;
    }

    void LateUpdate()
    {
        float aim = Ease(aimBlend);
        float lower = Ease(lowerBlend);

        Vector3 position = Vector3.Lerp(hipPosition, aimPosition, aim);
        Quaternion rotation = Quaternion.Slerp(Quaternion.Euler(hipRotation), Quaternion.Euler(aimRotation), aim);
        position = Vector3.Lerp(position, loweredPosition, lower);
        rotation = Quaternion.Slerp(rotation, Quaternion.Euler(loweredRotation), lower);

        position.z -= kickBack;
        rotation *= Quaternion.Euler(-kickPitch, 0f, 0f);
        transform.SetLocalPositionAndRotation(position, rotation);
    }

    /// <summary>Adds rounds to the reserve, for ammo pickups.</summary>
    public void AddAmmo(int rounds)
    {
        reserve += Mathf.Max(0, rounds);
    }

    void AdvanceState(float dt)
    {
        if (state == State.Ready)
            return;

        stateTime += dt;

        if (state == State.Reloading && !reloadAmmoMoved && stateTime >= stateDuration * data.reloadAmmoPoint)
        {
            int moved = Mathf.Min(data.magazineSize - magazine, reserve);
            magazine += moved;
            reserve -= moved;
            reloadAmmoMoved = true;
        }

        if (stateTime >= stateDuration)
            BeginState(State.Ready, 0f);
    }

    void BeginState(State next, float duration)
    {
        state = next;
        stateTime = 0f;
        stateDuration = duration;
        reloadAmmoMoved = false;
    }

    void TryReload()
    {
        if (magazine >= data.magazineSize || reserve <= 0)
            return;

        bool empty = magazine == 0;
        BeginState(State.Reloading, empty ? data.emptyReloadTime : data.reloadTime);
        if (animator != null)
            animator.CrossFadeInFixedTime(empty ? ReloadEmptyState : ReloadState, 0.08f, 0, 0f);
        Play(reloadClip);
    }

    void TryFire()
    {
        if (Time.time < nextFireTime || lowerBlend > 0.5f)
            return;
        if (data.requireAimToFire && aimBlend < 0.9f)
            return;

        if (magazine <= 0)
        {
            nextFireTime = Time.time + data.fireInterval;
            if (reserve > 0)
                TryReload();
            else
                Play(dryFireClip);
            return;
        }

        Fire();
    }

    void Fire()
    {
        magazine--;
        nextFireTime = Time.time + data.fireInterval;

        if (animator != null)
            animator.CrossFadeInFixedTime(FireState, 0.02f, 0, 0f);

        if (viewCamera != null)
        {
            Transform eye = viewCamera.transform;
            Vector2 offset = Random.insideUnitCircle * Mathf.Tan(SpreadDegrees * Mathf.Deg2Rad);
            Vector3 direction = (eye.forward + eye.right * offset.x + eye.up * offset.y).normalized;

            if (Physics.Raycast(eye.position, direction, out RaycastHit hit, data.range, data.hitMask, QueryTriggerInteraction.Ignore))
                ApplyHit(hit, direction);
        }

        if (look != null)
            look.AddRecoil(data.recoilPitch, Random.Range(-data.recoilYaw, data.recoilYaw));

        kickBack += data.kickBack;
        kickPitch += data.kickPitch;
        bloom = Mathf.Min(data.maxBloom, bloom + data.spreadPerShot);

        if (muzzleFlash != null)
            muzzleFlash.Emit(1);
        if (muzzleLight != null)
        {
            muzzleLight.enabled = true;
            muzzleLightOffTime = Time.time + muzzleLightTime;
        }

        Play(fireClip);
    }

    void ApplyHit(RaycastHit hit, Vector3 direction)
    {
        var damageable = hit.collider.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(new DamageInfo
            {
                amount = data.damage,
                point = hit.point,
                normal = hit.normal,
                direction = direction,
                source = gameObject,
            });
        }

        if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            hit.rigidbody.AddForceAtPosition(direction * data.impactForce, hit.point, ForceMode.Impulse);

        if (impactEffect != null)
        {
            impactEffect.transform.SetPositionAndRotation(hit.point + hit.normal * 0.01f, Quaternion.LookRotation(hit.normal));
            impactEffect.Emit(impactParticles);
        }
    }

    void UpdateSpread(float dt)
    {
        bloom = Mathf.MoveTowards(bloom, 0f, data.bloomRecovery * dt);
        float moving = movement != null ? Mathf.Clamp01(movement.HorizontalSpeed / 2f) : 0f;
        SpreadDegrees = Mathf.Lerp(data.hipSpread, data.aimSpread, aimBlend) + moving * data.movingSpread + bloom;
    }

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    static float Ease(float t)
    {
        return t * t * (3f - 2f * t);
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
