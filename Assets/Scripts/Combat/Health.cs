using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Hit points for an enemy or prop. Hitboxes on child colliders feed damage into this.
/// </summary>
[DisallowMultipleComponent]
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] float maxHealth = 100f;

    [Header("Events")]
    [SerializeField] UnityEvent onDamaged;
    [SerializeField] UnityEvent onDied;

    public event Action<DamageInfo> Damaged;
    public event Action<DamageInfo> Died;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsDead { get; private set; }

    void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || info.amount <= 0f)
            return;

        Current = Mathf.Max(0f, Current - info.amount);
        Damaged?.Invoke(info);
        onDamaged?.Invoke();

        if (Current > 0f)
            return;

        IsDead = true;
        Died?.Invoke(info);
        onDied?.Invoke();
    }

    public void Restore()
    {
        Current = maxHealth;
        IsDead = false;
    }
}
