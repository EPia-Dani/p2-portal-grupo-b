using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class PortalPreview : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private float maxDistance = 250f;

    [Header("Portal Reference")]
    [Tooltip("Portal usado como referencia de tamaño/validación. Asigna BluePortal u OrangePortal.")]
    [SerializeField] private Portal referencePortal; // <<< asigna en Inspector

    [Header("Shape & Draw")]
    [SerializeField, Range(12, 128)] private int segments = 64;
    [SerializeField] private float surfaceEpsilon = 0.01f;
    [SerializeField] private float lineWidth = 0.02f;
    [SerializeField] private Material lineMaterial; // Unlit/Color blanco recomendado

    [Header("Light Validation (fallback si no hay TryComputePlacement)")]
    [Tooltip("Incidencia mínima permitida. Para aceptar impactos frontales, usa 0–10.")]
    [SerializeField] private float minIncidenceDeg = 0f;
    [Tooltip("Capas que invalidan el emplazamiento por colisión (NO incluyas la capa de la pared colocable).")]
    [SerializeField] private LayerMask blockingMask = 0; // mejor Nothing por defecto

    // Estado público consumido por PortalPlacement
    public bool IsValid { get; private set; }
    public Vector3 PreviewPosition { get; private set; }
    public Quaternion PreviewRotation { get; private set; }
    public Collider Surface { get; private set; }
    public Vector3 HitPoint { get; private set; }

    // Internos
    private LineRenderer _lr;
    private Vector3[] _points;
    private MethodInfo _tryComputeMI; // reflection (opcional)
    private BoxCollider _refBox;

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        if (_lr == null) _lr = gameObject.AddComponent<LineRenderer>();

        _lr.useWorldSpace = true;
        _lr.loop = true;
        _lr.widthMultiplier = lineWidth;
        _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lr.receiveShadows = false;
        _lr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        _lr.numCornerVertices = 2;
        _lr.numCapVertices = 2;
        if (lineMaterial != null) _lr.material = lineMaterial;

        _points = new Vector3[segments];
        _lr.positionCount = segments;
        _lr.enabled = false;

        // Cache del collider del portal de referencia (para tamaño real)
        if (referencePortal != null)
        {
            _refBox = referencePortal.GetComponent<BoxCollider>();
            // Lookup opcional del método TryComputePlacement(Collider, Vector3, Quaternion, out Vector3, out Quaternion)
            _tryComputeMI = referencePortal.GetType().GetMethod(
                "TryComputePlacement",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Collider), typeof(Vector3), typeof(Quaternion), typeof(Vector3).MakeByRefType(), typeof(Quaternion).MakeByRefType() },
                null
            );
        }
        // Fuerza la capa del GO del LineRenderer a Default (visible con casi cualquier Culling Mask)
        gameObject.layer = LayerMask.NameToLayer("Default");
        // Asegura alineación en pantalla (mejor visibilidad del trazo)
        var lr = GetComponent<LineRenderer>();
        if (lr != null) lr.alignment = LineAlignment.View;
    }

    /// Tick externo: origen, dirección y mask de superficies colocables.
    public void Tick(Vector3 origin, Vector3 dir, LayerMask placementMask)
    {
        // Raycast principal
        if (!Physics.Raycast(origin, dir, out var hit, maxDistance, placementMask, QueryTriggerInteraction.Ignore))
        {
            SetInvalid();
            return;
        }

        // Ignorar portales (el disparo atraviesa)
        if (hit.collider.CompareTag("Portal"))
        {
            SetInvalid();
            return;
        }

        // Validación de incidencia (acepta 0–10 para impactos frontales)
        float incidence = Vector3.Angle(hit.normal, -dir);
        if (incidence < minIncidenceDeg)
        {
            SetInvalid();
            return;
        }

        // Base y rotación coherentes con tu PortalPlacement:
        // right ≈ "right" de la cámara proyectado en el plano de la superficie
        Vector3 cameraRightApprox = Vector3.Cross(dir, Vector3.up);
        if (cameraRightApprox.sqrMagnitude < 1e-6f) cameraRightApprox = Vector3.right;
        Vector3 right = Vector3.Normalize(Vector3.ProjectOnPlane(cameraRightApprox, hit.normal));
        Vector3 upOnSurface = Vector3.Normalize(Vector3.Cross(hit.normal, right));
        Quaternion initialRot = Quaternion.LookRotation(-hit.normal, upOnSurface);

        if (referencePortal != null && _tryComputeMI != null)
        {
            object[] args = new object[] { hit.collider, hit.point, initialRot, Vector3.zero, Quaternion.identity };
            bool ok = (bool)_tryComputeMI.Invoke(referencePortal, args);
            if (!ok)
            {
                SetInvalid();
                return;
            }

            Vector3 finalPos = (Vector3)args[3];
            Quaternion finalRot = (Quaternion)args[4];

            DrawOutline(finalPos, right, upOnSurface, referencePortal.transform, _refBox);
            PublishState(true, hit, finalPos, finalRot);
            return;
        }

        // --- MODO 2 (fallback): validación ligera + contorno ---
        // Overlap mínimo para no “pintar” en casos claramente inválidos
        Vector3 center = hit.point + hit.normal * surfaceEpsilon;
        Quaternion rot = initialRot;

        if (blockingMask != 0)
        {
            // tamaño desde BoxCollider si hay portal de referencia; si no, usa heurística
            GetHalfExtents(out float halfW, out float halfH);
            Vector3 halfExtents = new Vector3(halfW, halfH, surfaceEpsilon);
            var overlaps = Physics.OverlapBox(center, halfExtents, rot, blockingMask, QueryTriggerInteraction.Ignore);
            if (overlaps != null && overlaps.Length > 0)
            {
                SetInvalid();
                return;
            }
        }

        DrawOutline(center, right, upOnSurface, referencePortal != null ? referencePortal.transform : null, _refBox);
        PublishState(true, hit, center, rot);
    }

    private void PublishState(bool valid, RaycastHit hit, Vector3 pos, Quaternion rot)
    {
        IsValid = valid;
        Surface = valid ? hit.collider : null;
        HitPoint = hit.point;         // seguimos usando el punto de impacto para PlacePortal
        PreviewPosition = pos;        // usado solo para visualizar
        PreviewRotation = rot;        // se pasa a PlacePortal desde PortalPlacement
        _lr.enabled = valid;
    }

    private void DrawOutline(Vector3 center, Vector3 right, Vector3 upOnSurface, Transform refPortalTr, BoxCollider refBox)
    {
        // calcula half-width/half-height desde el BoxCollider del portal real (sin hardcodes)
        GetHalfExtents(out float halfW, out float halfH);

        float a = halfW;
        float b = halfH;

        for (int i = 0; i < segments; i++)
        {
            float t = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 local = new Vector3(Mathf.Cos(t) * a, Mathf.Sin(t) * b, 0f);
            Vector3 world = center + right * local.x + upOnSurface * local.y;
            _points[i] = world;
        }
        _lr.positionCount = segments;
        _lr.SetPositions(_points);
    }

    private void GetHalfExtents(out float halfW, out float halfH)
    {
        halfW = 0.55f; // fallback suave si no hay referencia (no se usan si hay refBox)
        halfH = 1.0f;

        if (_refBox != null)
        {
            // Convertimos size local del BoxCollider a mundo (escala incluida)
            Vector3 size = Vector3.Scale(_refBox.size, referencePortal.transform.lossyScale);
            halfW = 0.5f * size.x;
            halfH = 0.5f * size.y;
        }
    }

    public void Hide() => SetInvalid();

    private void SetInvalid()
    {
        IsValid = false;
        Surface = null;
        _lr.enabled = false;
    }
}