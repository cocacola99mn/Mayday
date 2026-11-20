using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aim reticle and ammo counter. The reticle only shows while the gun is raised, and its
/// gap matches the real bullet spread on screen.
/// </summary>
[DisallowMultipleComponent]
public class WeaponHUD : MonoBehaviour
{
    [SerializeField] PlayerWeapon weapon;

    [Header("Reticle")]
    [SerializeField] CanvasGroup reticle;
    [SerializeField] RectTransform top;
    [SerializeField] RectTransform bottom;
    [SerializeField] RectTransform left;
    [SerializeField] RectTransform right;
    [SerializeField, Tooltip("Smallest gap from the centre, in canvas units.")]
    float minimumGap = 6f;

    [Header("Ammo")]
    [SerializeField] Text ammoText;

    RectTransform canvasRect;
    int shownMagazine = -1;
    int shownReserve = -1;

    void Awake()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            canvasRect = canvas.transform as RectTransform;
    }

    void LateUpdate()
    {
        if (weapon == null)
            return;

        if (reticle != null)
            reticle.alpha = weapon.AimBlend;

        Camera view = weapon.ViewCamera;
        if (view != null && canvasRect != null)
        {
            float halfHeight = canvasRect.rect.height * 0.5f;
            float spread = Mathf.Tan(weapon.SpreadDegrees * Mathf.Deg2Rad);
            float halfView = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float gap = Mathf.Max(minimumGap, spread / halfView * halfHeight);

            Place(top, new Vector2(0f, gap));
            Place(bottom, new Vector2(0f, -gap));
            Place(left, new Vector2(-gap, 0f));
            Place(right, new Vector2(gap, 0f));
        }

        if (ammoText != null && (weapon.AmmoInMagazine != shownMagazine || weapon.ReserveAmmo != shownReserve))
        {
            shownMagazine = weapon.AmmoInMagazine;
            shownReserve = weapon.ReserveAmmo;
            ammoText.text = shownMagazine + " / " + shownReserve;
        }
    }

    static void Place(RectTransform tick, Vector2 position)
    {
        if (tick != null)
            tick.anchoredPosition = position;
    }
}
