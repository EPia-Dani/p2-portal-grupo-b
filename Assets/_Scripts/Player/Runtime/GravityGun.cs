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
    
    static readonly Quaternion HalfTurn = Quaternion.Euler(0,180,0);

    private IGrabbable grabbedObject;
    
    [SerializeField] private bool drawHoldRayGizmos = true;
    [SerializeField] private float gizmoSphereRadius = 0.08f;

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (grabbedObject == null)
            {
                TryPickup();
            }
            else
            {
                grabbedObject?.OnRelease();
                grabbedObject = null;
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
            grabbedObject?.OnRelease();
            grabbedObject = null;
        }
    }
    
    private void Shoot()
    {
        if (grabbedObject != null)
        {
            grabbedObject?.OnThrow(this);
            grabbedObject = null;
        }
    }
    
    private void TryPickup()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, grabRange))
        {
            IGrabbable grabbable = hitInfo.collider.GetComponent<IGrabbable>();
            if (grabbable != null)
            {
                grabbedObject = grabbable;
                grabbedObject?.OnGrab(this);
            }
        }
    }

    public void Release()
    {
        grabbedObject = null;
    }
    
    void LateUpdate() {
        if (grabbedObject == null) return;
        if (TryGetPortalAwareHoldPose(out var p, out var r)) {
            grabbedObject.SetTargetPose(p, r); // grabbable handles interpolation
        }
    }
    
    public bool TryGetPortalAwareHoldPose(out Vector3 pos, out Quaternion rot) {
        Vector3 o = playerCamera.transform.position;
        Vector3 d = playerCamera.transform.forward.normalized;
        Quaternion q = playerCamera.transform.rotation;
        const float EPS = 0.01f;

        for (int hops = 0; hops < 4; hops++) {
            // 1) Hit real geometry first
            if (Physics.Raycast(o, d, out var hit, holdDistance, hitMask, QueryTriggerInteraction.Ignore)) {
                pos = hit.point;
                rot = q;
                return true;
            }
            // 2) If no solid, check portal screen
            if (Physics.Raycast(o, d, out var phit, Mathf.Infinity, portalScreenMask, QueryTriggerInteraction.Collide)) {
                // Map ray through portal
                var portal = phit.collider.GetComponentInParent<Portal>();
                var other  = portal != null ? portal.OtherPortal : null;
                if (portal == null || other == null) break; // safety

                Transform inT  = portal.transform;
                Transform outT = other.transform;

                // Transport origin slightly past the exit plane
                Vector3 relP = inT.InverseTransformPoint(phit.point + d * EPS);
                relP = HalfTurn * relP;
                o = outT.TransformPoint(relP) + outT.forward * EPS;

                // Transport direction and rotation
                Vector3 relD = inT.InverseTransformDirection(d);
                relD = (HalfTurn * relD).normalized;
                d = outT.TransformDirection(relD).normalized;

                Quaternion deltaRot = outT.rotation * HalfTurn * Quaternion.Inverse(inT.rotation);
                q = deltaRot * q;
                
                continue;
            }
            // 3) Nothing hit: place at max distance in current space
            pos = o + d * holdDistance;
            rot = q;
            return true;
        }
        // Fallback after hop cap
        pos = o + d * holdDistance;
        rot = q;
        return true;
    }
    
    // ===== Gizmos for hold ray =====
    
    private void OnDrawGizmosSelected()
    {
        if (!drawHoldRayGizmos) return;

        GetPortalAwareRayPath(out var holdPos, out var holdRot, out var pts);

        // Path
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        for (int i = 0; i < pts.Count - 1; i++)
            Gizmos.DrawRay(pts[i], pts[i + 1]-pts[i]);

        // Hold sphere
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawSphere(holdPos, gizmoSphereRadius);

        // Forward from hold orientation (short tick)
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.9f);
        Gizmos.DrawLine(holdPos, holdPos + (holdRot * Vector3.forward) * 0.25f);
    }
    
    private void GetPortalAwareRayPath(out Vector3 holdPos, out Quaternion holdRot, out System.Collections.Generic.List<Vector3> points)
    {
        points = new System.Collections.Generic.List<Vector3>(8);

        Vector3 o = playerCamera ? playerCamera.transform.position : transform.position;
        Vector3 d = playerCamera ? playerCamera.transform.forward.normalized : transform.forward;
        Quaternion q = playerCamera ? playerCamera.transform.rotation : transform.rotation;
        const float EPS = 0.01f;

        points.Add(o);

        for (int hops = 0; hops < 4; hops++)
        {
            // 1) Hit real geometry first (limited by holdDistance)
            if (Physics.Raycast(o, d, out var hit, holdDistance, hitMask, QueryTriggerInteraction.Ignore))
            {
                points.Add(hit.point);
                holdPos = hit.point;
                holdRot = q;
                return;
            }

            // 2) Check portal "screen" to traverse
            if (Physics.Raycast(o, d, out var phit, Mathf.Infinity, portalScreenMask, QueryTriggerInteraction.Collide))
            {
                // draw up to the portal hit
                points.Add(phit.point);

                var portal = phit.collider.GetComponentInParent<Portal>();
                var other  = portal ? portal.OtherPortal : null;
                if (!portal || !other) break;

                Transform inT  = portal.transform;
                Transform outT = other.transform;

                // Transport origin slightly past the exit plane
                Vector3 relP = inT.InverseTransformPoint(phit.point + d * EPS);
                relP = HalfTurn * relP;
                o = outT.TransformPoint(relP) + outT.forward * EPS;

                // Transport direction and rotation
                Vector3 relD = inT.InverseTransformDirection(d);
                relD = (HalfTurn * relD).normalized;
                d = outT.TransformDirection(relD).normalized;

                Quaternion deltaRot = outT.rotation * HalfTurn * Quaternion.Inverse(inT.rotation);
                q = deltaRot * q;

                // Continue from new origin; also add a tiny step so the next draw segment starts visibly beyond the portal
                points.Add(o);
                continue;
            }

            // 3) Nothing hit: end at max distance in current space
            Vector3 end = o + d * holdDistance;
            points.Add(end);
            holdPos = end;
            holdRot = q;
            return;
        }

        // Fallback after hop cap
        Vector3 fallbackEnd = o + d * holdDistance;
        points.Add(fallbackEnd);
        holdPos = fallbackEnd;
        holdRot = q;
    }
}