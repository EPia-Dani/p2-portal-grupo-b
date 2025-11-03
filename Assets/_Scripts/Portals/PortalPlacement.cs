using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PortalPlacement : MonoBehaviour
{
    [SerializeField] private PortalPair portals;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Crosshair crosshair;
    [SerializeField] private Transform lookTransform;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private void Awake()
    {
        if (lookTransform == null) lookTransform = transform;
    }

    // ===== Legacy Input (solo si tienes Active Input Handling = Both) =====
    private void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            if (debugLogs) Debug.Log("[PortalPlacement] Legacy Fire1");
            FirePortal(0, lookTransform.position, lookTransform.forward, 250f);
        }
        else if (Input.GetButtonDown("Fire2"))
        {
            if (debugLogs) Debug.Log("[PortalPlacement] Legacy Fire2");
            FirePortal(1, lookTransform.position, lookTransform.forward, 250f);
        }
    }

    // ===== New Input System (PlayerInput → Invoke Unity Events) =====
    public void OnFireBlue(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;
        if (debugLogs) Debug.Log($"[PortalPlacement] FireBlue started. pos={lookTransform.position} dir={lookTransform.forward}");
        FirePortal(0, lookTransform.position, lookTransform.forward, 250f);
    }

    public void OnFireOrange(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;
        if (debugLogs) Debug.Log($"[PortalPlacement] FireOrange started. pos={lookTransform.position} dir={lookTransform.forward}");
        FirePortal(1, lookTransform.position, lookTransform.forward, 250f);
    }

    private void FirePortal(int portalID, Vector3 pos, Vector3 dir, float distance)
    {
        // Dibuja el rayo 2s en la Scene
        Debug.DrawRay(pos, dir * distance, Color.cyan, 2f);

        if (Physics.Raycast(pos, dir, out var hit, distance, layerMask, QueryTriggerInteraction.Ignore))
        {
            if (debugLogs)
                Debug.Log($"[PortalPlacement] HIT {hit.collider.name} (layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}) @ {hit.point}");

            // Disparo a través de portal
            if (hit.collider.CompareTag("Portal"))
            {
                var inPortal = hit.collider.GetComponent<Portal>();
                if (inPortal == null) { if (debugLogs) Debug.LogWarning("[PortalPlacement] Tag Portal sin componente Portal."); return; }
                var outPortal = inPortal.OtherPortal;
                if (outPortal == null) { if (debugLogs) Debug.LogWarning("[PortalPlacement] OtherPortal es null."); return; }

                Vector3 relativePos = inPortal.transform.InverseTransformPoint(hit.point + dir);
                relativePos = Quaternion.Euler(0f, 180f, 0f) * relativePos;
                pos = outPortal.transform.TransformPoint(relativePos);

                Vector3 relativeDir = inPortal.transform.InverseTransformDirection(dir);
                relativeDir = Quaternion.Euler(0f, 180f, 0f) * relativeDir;
                dir = outPortal.transform.TransformDirection(relativeDir);

                distance -= Vector3.Distance(pos, hit.point);
                if (debugLogs) Debug.Log($"[PortalPlacement] Re-shoot through portal. newPos={pos}, newDir={dir}, newDist={distance}");
                FirePortal(portalID, pos, dir, distance);
                return;
            }

            // Orientación del portal
            var cameraRotation = lookTransform.rotation;
            var portalRight = cameraRotation * Vector3.right;
            portalRight = (Mathf.Abs(portalRight.x) >= Mathf.Abs(portalRight.z))
                ? (portalRight.x >= 0 ? Vector3.right : -Vector3.right)
                : (portalRight.z >= 0 ? Vector3.forward : -Vector3.forward);

            var portalForward = -hit.normal;
            var portalUp = -Vector3.Cross(portalRight, portalForward);
            var portalRotation = Quaternion.LookRotation(portalForward, portalUp);

            bool wasPlaced = portals.Portals[portalID].PlacePortal(hit.collider, hit.point, portalRotation);
            if (debugLogs) Debug.Log($"[PortalPlacement] PlacePortal(id={portalID}) => {wasPlaced}");
            if (wasPlaced && crosshair != null) crosshair.SetPortalPlaced(portalID, true);
            if (!wasPlaced && debugLogs) Debug.LogWarning("[PortalPlacement] Rechazado por overlap/intersección/borde.");
        }
        else
        {
            if (debugLogs) Debug.Log("[PortalPlacement] MISS (no golpeó nada de la LayerMask).");
        }
    }
}