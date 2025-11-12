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
    
    [SerializeField] private float scrollScaleSensitivity = 0.15f; // per wheel step

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
    
    public void OnScalePortal(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || portalPreview == null) return;
        var scroll = ctx.ReadValue<Vector2>().y; // positive = zoom in by default
        var p = portalPreview.ReferencePortal;   // expose a getter
        if (p == null) return;
        p.SetDesiredScale(p.DesiredScale + scroll * scrollScaleSensitivity);
    }

    private void FirePortal(int portalID, Vector3 pos, Vector3 dir, float distance)
    {
        // If preview is valid, place directly from its precomputed pose
        if (portalPreview != null && portalPreview.IsValid && portalPreview.Surface != null)
        {
            portals.Portals[portalID].SetDesiredScale(portalPreview.ReferencePortal.DesiredScale);
            portals.Portals[portalID].PlacePrecomputed(
                portalPreview.Surface,
                portalPreview.PreviewPosition,
                portalPreview.PreviewRotation
            );
        }
    }
}