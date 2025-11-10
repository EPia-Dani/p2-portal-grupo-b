using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PortalPlacement : MonoBehaviour
{
    [SerializeField] private PortalPair portals;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Crosshair crosshair;
    [SerializeField] private Transform lookTransform;
    
    [Header("Preview")]
    [SerializeField] private PortalPreview portalPreview;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private void Awake()
    {
        if (lookTransform == null) lookTransform = transform;
        if (portalPreview == null) portalPreview = GetComponent<PortalPreview>(); 

    }
    
    private void LateUpdate()
    {
        if (portalPreview == null) return;
        portalPreview.Tick(lookTransform.position, lookTransform.forward, layerMask);
    }

    // ===== New Input System (PlayerInput → Invoke Unity Events) =====
    public void OnFireBlue(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            if (debugLogs) Debug.Log($"[PortalPlacement] FireBlue started. pos={lookTransform.position} dir={lookTransform.forward}");
            FirePortal(0, lookTransform.position, lookTransform.forward, 250f);
        }
        

    }

    public void OnFireOrange(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            if (debugLogs) Debug.Log($"[PortalPlacement] FireOrange started. pos={lookTransform.position} dir={lookTransform.forward}");
            FirePortal(1, lookTransform.position, lookTransform.forward, 250f);            
        }
    }

    private void FirePortal(int portalID, Vector3 pos, Vector3 dir, float distance)
{
    // Dibuja el rayo 2s en la Scene
    Debug.DrawRay(pos, dir * distance, Color.cyan, 2f);

    if (Physics.Raycast(pos, dir, out var hit, distance, layerMask, QueryTriggerInteraction.Ignore))
    {
        if (debugLogs)
            Debug.Log($"[PortalPlacement] HIT {hit.collider.name} (layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}) @ {hit.point}");

        // --- Disparo a través de portal: DESACTIVADO temporalmente ---
        // if (hit.collider.CompareTag("Portal"))
        // {
        //     if (debugLogs) Debug.Log("[PortalPlacement] Portal hit: through-shot disabled temporarily.");
        //     return;
        // }

        bool wasPlaced = false;

        // 1) Colocación via PREVIEW (misma superficie)
        if (portalPreview != null && portalPreview.IsValid && portalPreview.Surface == hit.collider)
        {
            wasPlaced = portals.Portals[portalID].PlacePortal(
                portalPreview.Surface,
                portalPreview.HitPoint,
                portalPreview.PreviewRotation
            );
            if (debugLogs) Debug.Log($"[PortalPlacement] PlacePortal(id={portalID}) via PREVIEW => {wasPlaced}");
        }

        // 2) Fallback: orientación original si no hay preview válido
        if (!wasPlaced)
        {
            var cameraRotation = lookTransform.rotation;
            var portalRight = cameraRotation * Vector3.right;
            // portalRight = (Mathf.Abs(portalRight.x) >= Mathf.Abs(portalRight.z))? (portalRight.x >= 0 ? Vector3.right : -Vector3.right): (portalRight.z >= 0 ? Vector3.forward : -Vector3.forward);

            var portalForward = -hit.normal;
            var portalUp = -Vector3.Cross(portalRight, portalForward);
            var portalRotation = Quaternion.LookRotation(portalForward, portalUp);

            wasPlaced = portals.Portals[portalID].PlacePortal(hit.collider, hit.point, portalRotation);
            if (debugLogs) Debug.Log($"[PortalPlacement] PlacePortal(id={portalID}) via FALLBACK => {wasPlaced}");
        }

        if (wasPlaced && crosshair != null) crosshair.SetPortalPlaced(portalID, true);
        if (!wasPlaced && debugLogs) Debug.LogWarning("[PortalPlacement] Rechazado por overlap/intersección/borde.");
    }
    else
    {
        if (debugLogs) Debug.Log("[PortalPlacement] MISS (no golpeó nada de la LayerMask).");
    }
}
}