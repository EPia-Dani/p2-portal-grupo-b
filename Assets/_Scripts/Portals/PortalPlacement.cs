using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PortalPlacement : MonoBehaviour
{
    [Serializable]
    public struct AimResult
    {
        public bool hasHit;
        public RaycastHit hit;
        public Quaternion initialRotation;

        public bool canBlue;
        public Vector3 bluePos;
        public Quaternion blueRot;

        public bool canOrange;
        public Vector3 orangePos;
        public Quaternion orangeRot;
    }

    [Header("Portals")]
    [SerializeField] private PortalPair portals;

    [Header("Raycast / Placement")]
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Transform lookTransform;
    [SerializeField] private float maxDistance = 250f;
    [Tooltip("Optional minimum incidence angle (in degrees). 0 = disabled.")]
    [SerializeField] private float minIncidenceDeg = 0f;

    [Header("References")]
    [SerializeField] private PortalPreview portalPreview;
    [SerializeField] private Crosshair crosshair;
    [SerializeField] private GravityGun gravityGun;

    [Header("Scroll Scaling")]
    [SerializeField] private float scrollScaleSensitivity = 0.15f; // per wheel step

    private bool isPreviewing;
    private int activePortalIndex = -1;

    private AimResult _aim;
    public AimResult Aim => _aim;

    private void Awake()
    {
        if (lookTransform == null) lookTransform = transform;
        if (portalPreview == null) portalPreview = GetComponent<PortalPreview>();
    }

    private void Start()
    {
        gravityGun ??= GetComponent<GravityGun>();
        if (crosshair != null)
            crosshair.SetPlacement(this);
    }

    private void LateUpdate()
    {
        // 1) Compute aim once per frame (raycast + TryComputePlacement for both portals)
        UpdateAim();

        // 2) Drive preview from the same data
        if (portalPreview == null) return;

        if (isPreviewing)
        {
            if (!_aim.hasHit || portals == null || portals.Portals == null)
            {
                portalPreview.Hide();
                return;
            }

            if (activePortalIndex == 0)
            {
                var p = portals.Portals.Length > 0 ? portals.Portals[0] : null;
                if (p != null && _aim.canBlue)
                {
                    // Small visual offset to pull the preview slightly off the wall
                    Vector3 pos = _aim.bluePos + _aim.hit.normal * (p.DesiredScale / 10f);
                    portalPreview.ShowFromAim(p, _aim.hit, pos, _aim.blueRot);
                }
                else
                {
                    portalPreview.Hide();
                }
            }
            else if (activePortalIndex == 1)
            {
                var p = portals.Portals.Length > 1 ? portals.Portals[1] : null;
                if (p != null && _aim.canOrange)
                {
                    Vector3 pos = _aim.orangePos + _aim.hit.normal * (p.DesiredScale / 10f);
                    portalPreview.ShowFromAim(p, _aim.hit, pos, _aim.orangeRot);
                }
                else
                {
                    portalPreview.Hide();
                }
            }
            else
            {
                portalPreview.Hide();
            }
        }
        else
        {
            portalPreview.Hide();
        }
    }

    private void UpdateAim()
    {
        _aim = default;

        if (lookTransform == null || portals == null || portals.Portals == null || portals.Portals.Length < 2)
            return;
        
        UpdateBlue();
        UpdateOrange();
    }

    private void UpdateBlue()
    {
        UpdatePortalForIndex(
            portalIndex: 0,
            canPortal: ref _aim.canBlue,
            pos: ref _aim.bluePos,
            rot: ref _aim.blueRot
        );
    }

    private void UpdateOrange()
    {
        UpdatePortalForIndex(
            portalIndex: 1,
            canPortal: ref _aim.canOrange,
            pos: ref _aim.orangePos,
            rot: ref _aim.orangeRot
        );
    }

    private void UpdatePortalForIndex(
        int portalIndex,
        ref bool canPortal,
        ref Vector3 pos,
        ref Quaternion rot
    )
    {
        var origin = lookTransform.position;
        var dir    = lookTransform.forward;
        
        var portal = portals.Portals[portalIndex];
        if (portal == null) return;
        
        int effectiveMask = layerMask;
        int selfLayer     = portal.gameObject.layer;
        effectiveMask &= ~(1 << selfLayer);
        
        if (!Physics.Raycast(origin, dir, out var hit, maxDistance, effectiveMask, QueryTriggerInteraction.Collide))
            return;
        
        if (hit.collider.CompareTag("Portal"))
            return;
        
        float incidence = Vector3.Angle(hit.normal, -dir);
        if (incidence < minIncidenceDeg)
            return;

        var initialRot = ComputeInitialRotation(dir, hit.normal);

        _aim.hasHit          = true;
        _aim.hit             = hit;
        _aim.initialRotation = initialRot;
        
        if (portals == null || portals.Portals == null) return;
        if (portalIndex < 0 || portalIndex >= portals.Portals.Length) return;

        if (portal.TryComputePlacement(
                hit.collider,
                hit.point,
                initialRot,
                portal.DesiredScale,
                out var p,
                out var r))
        {
            canPortal = true;
            pos       = p;
            rot       = r;
        }
    }

    private static Quaternion ComputeInitialRotation(Vector3 viewDir, Vector3 surfaceNormal)
    {
        // Same logic as before (Crosshair / PortalPreview):
        Vector3 rightFromCam = Vector3.Cross(viewDir, Vector3.up);
        if (rightFromCam.sqrMagnitude < 1e-6f)
            rightFromCam = Vector3.right;

        Vector3 right       = Vector3.Normalize(Vector3.ProjectOnPlane(rightFromCam, surfaceNormal));
        Vector3 upOnSurface = Vector3.Normalize(Vector3.Cross(surfaceNormal, right));
        return Quaternion.LookRotation(-surfaceNormal, upOnSurface);
    }

    // ===== New Input System (PlayerInput → Invoke Unity Events) =====
    public void OnFireBlue(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            if (gravityGun != null && gravityGun.IsGrabbingObject)
                return;

            activePortalIndex = 0;
            isPreviewing      = true;
        }

        if (ctx.canceled)
        {
            if (gravityGun != null && gravityGun.IsGrabbingObject)
                return;

            TryPlaceActivePortal();
            isPreviewing = false;
        }
    }

    public void OnFireOrange(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            if (gravityGun != null && gravityGun.IsGrabbingObject)
                return;

            activePortalIndex = 1;
            isPreviewing      = true;
        }

        if (ctx.canceled)
        {
            if (gravityGun != null && gravityGun.IsGrabbingObject)
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
        if (target == null) return;

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
        var p      = portalPreview.ReferencePortal;
        if (p == null) return;

        p.SetDesiredScale(p.DesiredScale + scroll * scrollScaleSensitivity);
    }
}
