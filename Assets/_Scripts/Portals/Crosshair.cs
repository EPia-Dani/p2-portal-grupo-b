using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Crosshair : MonoBehaviour
{
    [Header("Placement Source")]
    [SerializeField] private PortalPlacement placement;

    [Header("UI")]
    [Tooltip("Base reticle always visible (no colors).")]
    [SerializeField] private Image baseImg;
    [Tooltip("Overlay for the blue portal indicator.")]
    [SerializeField] private Image blueImg;
    [Tooltip("Overlay for the orange portal indicator.")]
    [SerializeField] private Image orangeImg;

    public void SetPlacement(PortalPlacement p)
    {
        placement = p;
    }

    private void Update()
    {
        if (placement == null)
        {
            ApplyState(false, false);
            return;
        }

        var aim = placement.Aim;

        // If there's no valid hit, both are false
        bool canBlue   = aim.hasHit && aim.canBlue;
        bool canOrange = aim.hasHit && aim.canOrange;

        ApplyState(canBlue, canOrange);
    }

    private void ApplyState(bool canBlue, bool canOrange)
    {
        if (baseImg != null)
            baseImg.enabled = true;

        if (blueImg != null)
            blueImg.enabled = canBlue;

        if (orangeImg != null)
            orangeImg.enabled = canOrange;
    }
}