using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class PortalPreview : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private float maxDistance = 250f;

    [Header("Portal Reference")]
    [Tooltip("Asigna el Portal real para usar su lógica y tamaño.")]
    [SerializeField] private Portal referencePortal;
    
    public Portal ReferencePortal => referencePortal;

    [Header("Draw")]
    [SerializeField, Range(12, 128)] private int segments = 64;
    [SerializeField] private float lineWidth = 0.02f;
    [SerializeField] private Material lineMaterial;

    [Header("Fallback guard (solo si no hay portal)")]
    [SerializeField] private float minIncidenceDeg = 0f;
    [SerializeField] private LayerMask blockingMask = 0;

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
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.widthMultiplier = lineWidth;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.alignment = LineAlignment.View;
        if (lineMaterial != null) lr.material = lineMaterial;

        points = new Vector3[segments];
        lr.positionCount = segments;
        lr.enabled = false;
    }

    public void Tick(Vector3 origin, Vector3 dir, LayerMask placementMask)
    {
        // Ray principal contra superficies colocables
        if (!Physics.Raycast(origin, dir, out var hit, maxDistance, placementMask, QueryTriggerInteraction.Collide))
        {
            SetInvalid();
            return;
        }

        // Ignorar portales existentes
        if (hit.collider.CompareTag("Portal"))
        {
            SetInvalid();
            return;
        }

        // Incidencia mínima opcional
        float incidence = Vector3.Angle(hit.normal, -dir);
        if (incidence < minIncidenceDeg)
        {
            SetInvalid();
            return;
        }

        // Base de orientación: el forward del portal apunta hacia la pared
        // Construimos un "up" tangente estable proyectando Vector3.up en el plano
        Vector3 rightFromCam = Vector3.Cross(dir, Vector3.up);
        if (rightFromCam.sqrMagnitude < 1e-6f) rightFromCam = Vector3.right;
        Vector3 right = Vector3.Normalize(Vector3.ProjectOnPlane(rightFromCam, hit.normal));
        Vector3 upOnSurface = Vector3.Normalize(Vector3.Cross(hit.normal, right));
        Quaternion initialRot = Quaternion.LookRotation(-hit.normal, upOnSurface);

        // Ruta principal: usar la lógica unificada del Portal
        if (referencePortal != null)
        {
            if (!referencePortal.TryComputePlacement(hit.collider, hit.point, initialRot, referencePortal.DesiredScale,
                                                     out var finalPos, out var finalRot))
            {
                SetInvalid();
                return;
            }
            finalPos+= hit.normal * (referencePortal.DesiredScale/10f); 
            DrawEllipse(finalPos, finalRot);
            PublishState(true, hit, finalPos, finalRot);
        }
    }

    void DrawEllipse(Vector3 center, Quaternion rot)
    {
        GetHalfExtents(out float a, out float b);
        // Deriva ejes locales a partir de la rotación final
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
        halfW = 0.55f; halfH = 1.0f;
        if (referencePortal != null)
            referencePortal.GetHalfExtentsForScale(referencePortal.DesiredScale, out halfW, out halfH);
    }

    void PublishState(bool valid, RaycastHit hit, Vector3 pos, Quaternion rot)
    {
        IsValid = valid;
        Surface = valid ? hit.collider : null;
        HitPoint = hit.point;
        PreviewPosition = pos;
        PreviewRotation = rot;
    }

    public void Hide() => SetInvalid();

    void SetInvalid()
    {
        IsValid = false;
        Surface = null;
        lr.enabled = false;
    }
    
    public void SetReferencePortal(Portal p)
    {
        referencePortal = p;
        lr.material.color = p.PortalColor;
    }
}
