using UnityEngine;
using UnityEngine.InputSystem;

public class GravityGun : MonoBehaviour
{
    [SerializeField] private float grabRange = 8f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform holdPoint; // create a child empty GameObject ~2m ahead
    [SerializeField] private float holdDistance = 2f;
    [SerializeField] private LayerMask hitMask;
    [SerializeField] private LayerMask portalScreenMask;
    [SerializeField] private LayerMask grabbableMask;
    
    static readonly Quaternion HalfTurn = Quaternion.Euler(0,180,0);

    private IGrabbable _grabbedObject;
    
    [SerializeField] private bool drawHoldRayGizmos = true;
    [SerializeField] private float gizmoSphereRadius = 0.08f;

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (_grabbedObject == null)
            {
                TryPickup();
            }
            else
            {
                _grabbedObject?.OnRelease();
                _grabbedObject = null;
            }
        }
    }
    
    public void OnPrimary(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            Shoot();
        }
    }

    public void OnSecondary(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            _grabbedObject?.OnRelease();
            _grabbedObject = null;
        }
    }
    
    private void Shoot()
    {
        if (_grabbedObject != null)
        {
            _grabbedObject?.OnThrow(this);
            _grabbedObject = null;
        }
    }
    
    private void TryPickup()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, grabRange, grabbableMask, QueryTriggerInteraction.Ignore))
        {
            IGrabbable grabbable = hitInfo.collider.GetComponent<IGrabbable>();
            if (grabbable != null)
            {
                _grabbedObject = grabbable;
                _grabbedObject?.OnGrab(this);
            }
        }
    }

    public void Release()
    {
        _grabbedObject = null;
    }
    
    void LateUpdate() {
        if (_grabbedObject == null) return;
        if (TryGetPortalAwareHoldPose(out var p, out var r)) {
            _grabbedObject.SetTargetPose(p, r); // grabbable handles interpolation
        }
    }
    
    private const int MaxHops = 1;
    private const float EPS = 0.01f;

    private bool TraceHoldRay(bool collectPoints, out Vector3 holdPos, out Quaternion holdRot,
                              out System.Collections.Generic.List<Vector3> points)
    {
        points = collectPoints ? new System.Collections.Generic.List<Vector3>(8) : null;

        Transform camT = playerCamera ? playerCamera.transform : transform;
        Vector3 o = camT.position;
        Vector3 d = camT.forward.normalized;
        Quaternion q = camT.rotation;

        if (collectPoints) points.Add(o);

        float remaining = holdDistance;
        int combinedMask = hitMask | portalScreenMask;

        for (int hops = 0; hops < MaxHops; hops++)
        {
            // Nearest-hit wins among solids and portal screens
            if (!Physics.Raycast(o, d, out var hit, remaining, combinedMask, QueryTriggerInteraction.Collide))
            {
                // nothing within remaining distance
                Vector3 end = o + d * remaining;
                if (collectPoints) points.Add(end);
                holdPos = end;
                holdRot = q;
                return true;
            }

            if (collectPoints) points.Add(hit.point);

            // Portal screen?
            bool hitIsPortal = ((portalScreenMask.value & (1 << hit.collider.gameObject.layer)) != 0);
            if (hitIsPortal)
            {
                var portal = hit.collider.GetComponentInParent<Portal>();
                var other  = portal ? portal.OtherPortal : null;
                if (!portal || !other) break;

                // consume distance to the screen
                remaining -= hit.distance;

                // map origin and direction using Portal helpers (scale + center-aware)
                o = portal.MapPointToOther(hit.point, d, enterOffset: EPS, exitBackoff: 0.06f);
                d = portal.MapDirectionToOther(d);

                // map rotation using mapped forward/up through the portal
                Vector3 fW = portal.MapDirectionToOther(q * Vector3.forward);
                Vector3 uW = portal.MapDirectionToOther(q * Vector3.up);
                uW = Vector3.ProjectOnPlane(uW, fW).normalized;
                q = Quaternion.LookRotation(fW.normalized, uW);

                if (collectPoints) points.Add(o);
                continue; // keep tracing with remaining distance
            }

            // Solid hit first → place hold here
            holdPos = hit.point;
            holdRot = q;
            return true;
        }

        // Fallback after hop cap
        Vector3 fallback = o + d * remaining;
        if (collectPoints) points.Add(fallback);
        holdPos = fallback;
        holdRot = q;
        return true;
    }


    // ---- Public API used by gameplay ----
    public bool TryGetPortalAwareHoldPose(out Vector3 pos, out Quaternion rot)
    {
        return TraceHoldRay(false, out pos, out rot, out _);
    }

    // ---- Gizmos use the exact same tracer ----
    private void OnDrawGizmosSelected()
    {
    if (!drawHoldRayGizmos) return;

    if (TraceHoldRay(true, out var holdPos, out var holdRot, out var pts))
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        for (int i = 0; i < pts.Count - 1; i++)
            Gizmos.DrawLine(pts[i], pts[i + 1]);

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawSphere(holdPos, gizmoSphereRadius);

        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.9f);
        Gizmos.DrawLine(holdPos, holdPos + (holdRot * Vector3.forward) * 0.25f);
    }
}
}