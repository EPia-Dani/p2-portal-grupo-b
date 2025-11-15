using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class PortalPreview : MonoBehaviour
{
    [Header("Draw")]
    [SerializeField, Range(12, 128)] private int segments = 64;
    [SerializeField] private float lineWidth = 0.02f;
    [SerializeField] private Material lineMaterial;

    [Header("Portal Reference")]
    [SerializeField] private Portal referencePortal;
    public Portal ReferencePortal => referencePortal;

    // Estado que consumen otros scripts
    public bool IsValid { get; private set; }
    public Vector3 PreviewPosition { get; private set; }
    public Quaternion PreviewRotation { get; private set; }
    public Collider Surface { get; private set; }
    public Vector3 HitPoint { get; private set; }

    LineRenderer lr;
    Vector3[] points;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace               = true;
        lr.loop                        = true;
        lr.widthMultiplier             = lineWidth;
        lr.shadowCastingMode           = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows              = false;
        lr.motionVectorGenerationMode  = MotionVectorGenerationMode.ForceNoMotion;
        lr.numCornerVertices           = 2;
        lr.numCapVertices              = 2;
        lr.alignment                   = LineAlignment.View;
        if (lineMaterial != null) lr.material = lineMaterial;

        points          = new Vector3[segments];
        lr.positionCount = segments;
        lr.enabled       = false;
    }

    /// <summary>
    /// Called by PortalPlacement once per frame when previewing.
    /// Uses the already computed aim data and portal placement.
    /// </summary>
    public void ShowFromAim(Portal p, RaycastHit hit, Vector3 pos, Quaternion rot)
    {
        referencePortal = p;

        if (referencePortal != null && lr != null && lr.material != null)
            lr.material.color = referencePortal.PortalColor;

        DrawEllipse(pos, rot);
        PublishState(true, hit, pos, rot);
    }

    void DrawEllipse(Vector3 center, Quaternion rot)
    {
        GetHalfExtents(out float a, out float b);

        Vector3 rx = rot * Vector3.right;
        Vector3 ry = rot * Vector3.up;

        for (int i = 0; i < segments; i++)
        {
            float t = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 local = new Vector3(Mathf.Cos(t) * a, Mathf.Sin(t) * b, 0f);
            points[i] = center + rx * local.x + ry * local.y;
        }

        lr.positionCount = segments;
        lr.SetPositions(points);
        lr.enabled = true;
    }

    void GetHalfExtents(out float halfW, out float halfH)
    {
        // defaults
        halfW = 0.55f; 
        halfH = 1.0f;

        if (referencePortal != null)
            referencePortal.GetHalfExtentsForScale(referencePortal.DesiredScale, out halfW, out halfH);
    }

    void PublishState(bool valid, RaycastHit hit, Vector3 pos, Quaternion rot)
    {
        IsValid         = valid;
        Surface         = valid ? hit.collider : null;
        HitPoint        = hit.point;
        PreviewPosition = pos;
        PreviewRotation = rot;
    }

    public void Hide() => SetInvalid();

    void SetInvalid()
    {
        IsValid   = false;
        Surface   = null;
        lr.enabled = false;
    }
}
