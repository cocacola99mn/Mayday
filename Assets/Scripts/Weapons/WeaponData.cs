using UnityEngine;

/// <summary>
/// Tuning values for one firearm. Create more from Assets > Create > Tower Crawler > Weapon Data.
/// </summary>
[CreateAssetMenu(menuName = "Tower Crawler/Weapon Data", fileName = "WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("Damage")]
    public float damage = 25f;
    public float range = 60f;
    [Tooltip("Impulse applied to a Rigidbody that gets hit.")]
    public float impactForce = 4f;
    [Tooltip("Layers a bullet can hit. Leave the Player and Weapon layers out.")]
    public LayerMask hitMask = ~0;

    [Header("Fire")]
    [Tooltip("Minimum seconds between shots. One shot per press.")]
    public float fireInterval = 0.28f;
    [Tooltip("When on, the trigger does nothing unless the gun is raised.")]
    public bool requireAimToFire;

    [Header("Ammo")]
    public int magazineSize = 10;

    [Header("Timing")]
    [Tooltip("Seconds before the gun can be used after it is drawn.")]
    public float drawTime = 1f;
    public float reloadTime = 1.13f;
    public float emptyReloadTime = 1.62f;
    [Range(0.1f, 1f), Tooltip("Point in the reload where the new magazine counts as loaded.")]
    public float reloadAmmoPoint = 0.7f;

    [Header("Spread (degrees)")]
    public float hipSpread = 3.5f;
    public float aimSpread = 0.35f;
    [Tooltip("Added at full walking speed.")]
    public float movingSpread = 1.5f;
    [Tooltip("Added by each shot, then recovered over time.")]
    public float spreadPerShot = 1.2f;
    public float maxBloom = 3f;
    [Tooltip("Degrees of bloom recovered per second.")]
    public float bloomRecovery = 5f;

    [Header("Recoil")]
    [Tooltip("Degrees the view kicks up per shot.")]
    public float recoilPitch = 2.2f;
    [Tooltip("Largest sideways kick in degrees. The direction is random.")]
    public float recoilYaw = 0.6f;
    [Tooltip("Metres the gun jumps back toward the camera.")]
    public float kickBack = 0.015f;
    [Tooltip("Degrees the gun tips up, on top of the fire animation.")]
    public float kickPitch = 1.5f;

    [Header("Aim")]
    public float aimFieldOfView = 50f;
    [Tooltip("Seconds to raise or lower the gun.")]
    public float aimTime = 0.18f;
}
