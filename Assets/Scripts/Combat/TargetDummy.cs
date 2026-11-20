using System.Collections;
using UnityEngine;

/// <summary>
/// Practice target. Flashes when hit, drops out of sight when killed, then comes back.
/// </summary>
[RequireComponent(typeof(Health))]
public class TargetDummy : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    [SerializeField] Color hitColor = new Color(1f, 0.15f, 0.1f);
    [SerializeField] float flashTime = 0.12f;
    [SerializeField] float respawnDelay = 3f;

    Health health;
    Renderer[] renderers;
    Collider[] colliders;
    MaterialPropertyBlock block;
    float flashUntil;
    bool flashing;

    void Awake()
    {
        health = GetComponent<Health>();
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        block = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    void Update()
    {
        if (flashing && Time.time >= flashUntil)
            SetFlash(false);
    }

    void OnDamaged(DamageInfo info)
    {
        flashUntil = Time.time + flashTime;
        SetFlash(true);
    }

    void OnDied(DamageInfo info)
    {
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        SetVisible(false);
        yield return new WaitForSeconds(respawnDelay);
        health.Restore();
        SetFlash(false);
        SetVisible(true);
    }

    void SetVisible(bool visible)
    {
        foreach (var r in renderers)
            r.enabled = visible;
        foreach (var c in colliders)
            c.enabled = visible;
    }

    void SetFlash(bool on)
    {
        flashing = on;
        foreach (var r in renderers)
        {
            if (on)
            {
                block.SetColor(BaseColor, hitColor);
                r.SetPropertyBlock(block);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }
}
