using UnityEngine;

/// <summary>
/// One hit, as seen by whatever was struck.
/// </summary>
public struct DamageInfo
{
    public float amount;
    public Vector3 point;
    public Vector3 normal;
    public Vector3 direction;
    public GameObject source;
}

/// <summary>
/// Anything a bullet can hurt. The weapon looks for this on the collider it hit, then on its parents.
/// </summary>
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}
