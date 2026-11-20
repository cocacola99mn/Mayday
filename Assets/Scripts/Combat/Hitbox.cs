using UnityEngine;

/// <summary>
/// Put one on each body-part collider. Scales the damage, then passes it to the Health above.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Hitbox : MonoBehaviour, IDamageable
{
    [SerializeField, Tooltip("Leave empty to use the Health on a parent.")]
    Health health;

    [SerializeField, Tooltip("3 for a head, 1 for the torso, less for limbs.")]
    float damageMultiplier = 1f;

    void Awake()
    {
        if (health == null)
            health = GetComponentInParent<Health>();
    }

    public void TakeDamage(DamageInfo info)
    {
        if (health == null)
            return;

        info.amount *= damageMultiplier;
        health.TakeDamage(info);
    }
}
