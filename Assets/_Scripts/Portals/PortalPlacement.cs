using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PortalPlacement : MonoBehaviour
{
    [SerializeField] private PortalPair portals;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Crosshair crosshair;
    [SerializeField] private Transform lookTransform;
    [SerializeField] private GravityGun gravityGun;
    
    [Header("Preview")]
    [SerializeField] private PortalPreview portalPreview;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    
    [SerializeField] private float scrollScaleSensitivity = 0.15f; // per wheel step
    
    private bool isPreviewing;
    private int activePortalIndex = -1;

    private void Awake()
    {
        if (lookTransform == null) lookTransform = transform;
        if (portalPreview == null) portalPreview = GetComponent<PortalPreview>(); 
    }

    private void Start()
    {
        gravityGun ??= GetComponent<GravityGun>();
    }

    private void LateUpdate()
    {
        if (portalPreview == null) return;

        if (isPreviewing)
        {
            portalPreview.Tick(lookTransform.position, lookTransform.forward, layerMask);
        }
        else
        {
            portalPreview.Hide();
        }
    }

    // ===== New Input System (PlayerInput → Invoke Unity Events) =====
    public void OnFireBlue(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            if (gravityGun.IsGrabbingObject)
                return;
            activePortalIndex = 0;
            isPreviewing = true;
            if (portalPreview != null && portals != null)
                portalPreview.SetReferencePortal(portals.Portals[0]);
        }

        if (ctx.canceled)
        {
            if (gravityGun.IsGrabbingObject)
                return;
            TryPlaceActivePortal();
            isPreviewing = false;
        }
    }

    public void OnFireOrange(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            if (gravityGun.IsGrabbingObject)
                return;
            activePortalIndex = 1;
            isPreviewing = true;
            if (portalPreview != null && portals != null)
                portalPreview.SetReferencePortal(portals.Portals[1]);
        }

        if (ctx.canceled)
        {
            if (gravityGun.IsGrabbingObject)
                return;
            TryPlaceActivePortal();
            isPreviewing = false;
        }
    }

    private void TryPlaceActivePortal()
    {
        if (portalPreview == null || !portalPreview.IsValid || activePortalIndex < 0)
            return;

        var target = portals.Portals[activePortalIndex];
        target.SetDesiredScale(portalPreview.ReferencePortal.DesiredScale);
        target.PlacePrecomputed(
            portalPreview.Surface,
            portalPreview.PreviewPosition,
            portalPreview.PreviewRotation
        );
    }
    
    public void OnScalePortal(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || portalPreview == null || !isPreviewing) return;

        var scroll = ctx.ReadValue<Vector2>().y;
        var p = portalPreview.ReferencePortal;
        if (p == null) return;

        p.SetDesiredScale(p.DesiredScale + scroll * scrollScaleSensitivity);
    }

}