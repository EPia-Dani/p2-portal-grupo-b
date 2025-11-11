using System.Collections.Generic;
using _Scripts.Portals;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Portal : MonoBehaviour
{
    [field: SerializeField] public Portal OtherPortal { get; private set; }
    
    [SerializeField] private Renderer outlineRenderer;
    [field: SerializeField] public Color PortalColour { get; private set; }
    
    [SerializeField] private Renderer screenRenderer; 
    
    [SerializeField] private LayerMask placementMask;
    
    [SerializeField] private Transform testTransform;
    
    
    [Header("Placement (auto)")]
    [SerializeField] private float supportDepthFactor = 0.08f;   // % de halfHeight hacia la pared (antes 0.1)
    [SerializeField] private float edgeExtraFactor   = 0.05f;    // % de halfWidth/halfHeight (antes +0.1)
    [SerializeField] private float probeRadiusFactor = 0.02f;    // % de min(halfWidth, halfHeight) (antes 0.05)
    [SerializeField] private float overlapThicknessFactor = 0.02f; // % de halfHeight (antes 0.05)
    [SerializeField] private float forwardProbeFactor = 0.10f;   // % de halfHeight (antes 0.2)

    public enum EdgePolicy { Strict4of4, Majority3of4, Coverage }

    [Header("Placement Policy")]
    [SerializeField] private EdgePolicy edgePolicy = EdgePolicy.Coverage;
    [SerializeField, Range(16, 128)] private int supportSamples = 48;        // muestreo perímetro
    [SerializeField, Range(0.5f, 1f)] private float supportCoverageMin = 0.85f; // % cobertura mínimo
    [SerializeField, Range(0f, 45f)] private float curvatureToleranceDeg = 12f;  // opcional
    
    private float HalfWidth  => 0.5f * _collider.size.x * transform.lossyScale.x;
    private float HalfHeight => 0.5f * _collider.size.y * transform.lossyScale.y;

    private float EdgeExtra        => Mathf.Max(Physics.defaultContactOffset, edgeExtraFactor * Mathf.Min(HalfWidth, HalfHeight));
    private float FaceOffset       => Mathf.Max(Physics.defaultContactOffset, supportDepthFactor   * HalfHeight);
    private float CornerProbeRadius=> Mathf.Max(Physics.defaultContactOffset, probeRadiusFactor    * Mathf.Min(HalfWidth, HalfHeight));
    private float OverlapThickness => Mathf.Max(Physics.defaultContactOffset, overlapThicknessFactor * HalfHeight);
    private float ForwardProbe     => Mathf.Max(Physics.defaultContactOffset, forwardProbeFactor   * HalfHeight);
    
    private readonly List<PortalableBase> _portalables = new();



    public bool IsPlaced { get; private set; } = false;
    private Collider _wallCollider;
    public Collider WallCollider => _wallCollider;
    
    [SerializeField] private GameObject portalCollider;
    public GameObject PortalCollider => portalCollider;
    
    private Renderer _fallbackRenderer;              
    public Renderer Renderer => screenRenderer != null ? screenRenderer : _fallbackRenderer;
    private BoxCollider _collider;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        _collider.isTrigger = true;                 
        _fallbackRenderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        if (outlineRenderer != null)
        {
            outlineRenderer.material.SetColor("_OutlineColour", PortalColour);
        }
        if (Renderer != null)
            Renderer.enabled = false;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        // Mostrar/ocultar la pantalla del portal en función del estado del otro portal
        bool visible = (OtherPortal != null && OtherPortal.IsPlaced);
        if (Renderer != null) Renderer.enabled = visible;

        // DEBUG: estado de visibilidad (no spamea si no cambia)
        Debug.Log($"[Portal:{name}] Visible={visible} (OtherPortal={(OtherPortal ? OtherPortal.name : "null")})");
        foreach (var p in _portalables)
        {
            if (p == null) continue;
            if (p.HasCrossedPlane(this) && OtherPortal != null)
                p.Warp();
        }
        
        Debug.DrawRay(transform.position, transform.forward * 2.0f, Color.green);
    }

    private void OnTriggerEnter(Collider other)
    {
        var p = other.GetComponentInParent<PortalableBase>();
        if (p != null)
        {
            if (!_portalables.Contains(p))
                _portalables.Add(p);

            p.SetIsInPortal(this, OtherPortal);
            return;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var p = other.GetComponentInParent<PortalableBase>();
        if (p != null && _portalables.Contains(p))
        {
            _portalables.Remove(p);
            p.ExitPortal();
        }
    }
    
    public bool PlacePortal(Collider wallCollider, Vector3 pos, Quaternion rot)
    {
        // Pre-posicionado de prueba
        testTransform.position = pos;
        testTransform.rotation = rot;
        testTransform.position -= testTransform.forward * Mathf.Max(Physics.defaultContactOffset, 0.5f * OverlapThickness);
        // Ajustes de borde/intersecciones
        FixOverhangs();
        FixIntersects();

        bool ok = CheckOverlap();
        Debug.Log($"[Portal:{name}] PlacePortal en {pos} rot={rot.eulerAngles} -> {(ok ? "OK" : "FALLO (overlap/intersección/borde)")}");

        if (ok)
        {
            this._wallCollider = wallCollider;
            transform.position = testTransform.position;
            transform.rotation = testTransform.rotation;

            gameObject.SetActive(true);
            IsPlaced = true;

            // Forzar que la pantalla solo se vea si el otro está colocado
            if (Renderer != null)
                Renderer.enabled = (OtherPortal != null && OtherPortal.IsPlaced);

            return true;
        }

        return false;
    }

    private void FixOverhangs()
    {
        var testPoints = new List<Vector3>
        {
            new Vector3(-(HalfWidth  + EdgeExtra), 0.0f,  FaceOffset),
            new Vector3( (HalfWidth  + EdgeExtra), 0.0f,  FaceOffset),
            new Vector3( 0.0f, -(HalfHeight + EdgeExtra), FaceOffset),
            new Vector3( 0.0f,  (HalfHeight + EdgeExtra), FaceOffset)
        };

        var testDirs = new List<Vector3> { Vector3.right, -Vector3.right, Vector3.up, -Vector3.up };

        for (int i = 0; i < 4; ++i)
        {
            Vector3 raycastPos = testTransform.TransformPoint(testPoints[i]);
            Vector3 raycastDir = testTransform.TransformDirection(testDirs[i]);

            if (Physics.CheckSphere(raycastPos, CornerProbeRadius, placementMask))
                if (Physics.CheckSphere(raycastPos, CornerProbeRadius, placementMask))
                    continue; ;

            float dist = (i < 2) ? (HalfWidth + EdgeExtra) * 2f : (HalfHeight + EdgeExtra) * 2f;
            if (Physics.Raycast(raycastPos, raycastDir, out var hit, dist, placementMask))
            {
                var offset = hit.point - raycastPos;
                testTransform.Translate(offset, Space.World);
            }
        }
        NudgeForSupport();
    }
    

    private void FixIntersects()
    {
        var testDirs  = new List<Vector3> { Vector3.right, -Vector3.right, Vector3.up, -Vector3.up };
        var testDists = new List<float>   { HalfWidth + EdgeExtra, HalfWidth + EdgeExtra, HalfHeight + EdgeExtra, HalfHeight + EdgeExtra };

        for (int i = 0; i < 4; ++i)
        {
            Vector3 raycastPos = testTransform.TransformPoint(0.0f, 0.0f, -FaceOffset);
            Vector3 raycastDir = testTransform.TransformDirection(testDirs[i]);

            if (Physics.Raycast(raycastPos, raycastDir, out var hit, testDists[i], placementMask))
            {
                var offset    = (hit.point - raycastPos);
                var newOffset = -raycastDir * (testDists[i] - offset.magnitude);
                testTransform.Translate(newOffset, Space.World);
            }
        }
        NudgeForSupport();
    }

    private bool CheckOverlap()
    {
        // 1) El volumen del portal no debe penetrar de forma relevante.
        if (VolumeIntersects()) return false;

        // 2) Criterio de apoyo en superficie según política elegida.
        switch (edgePolicy)
        {
            case EdgePolicy.Strict4of4:
                return CornersSupport(4);     // 4/4 esquinas “tocan”
            case EdgePolicy.Majority3of4:
                return CornersSupport(3);     // 3/4 esquinas “tocan”
            case EdgePolicy.Coverage:
            default:
                // Cobertura por muestreo denso del perímetro + curvatura opcional
                bool coverOK = HasSufficientSupport();
                bool curveOK = LocalCurvatureOK(); // si no quieres curvatura, devuelve true
                return coverOK && curveOK;
        }
    }
    private bool VolumeIntersects()
    {
        // caja del portal “pegada” al plano: centro ligeramente detrás
        float slab = Mathf.Max(Physics.defaultContactOffset, 0.5f * OverlapThickness);
        Vector3 center = testTransform.position - testTransform.forward * slab;
        Vector3 halfExtents = new Vector3(HalfWidth, HalfHeight, slab);

        var cols = Physics.OverlapBox(center, halfExtents, testTransform.rotation, placementMask, QueryTriggerInteraction.Ignore);

        // tolerancia de penetración: 2×contactOffset o 2% del tamaño
        float allowance = Mathf.Max(2f * Physics.defaultContactOffset, 0.02f * Mathf.Min(HalfWidth, HalfHeight));

        foreach (var c in cols)
        {
            if (c == _collider) continue; // ignora tu propio collider

            // calcula penetración real; si es > allowance, entonces sí rechazamos
            if (Physics.ComputePenetration(
                    _collider, center, testTransform.rotation,
                    c, c.transform.position, c.transform.rotation,
                    out _, out float distance))
            {
                if (distance > allowance)
                    return true; 
            }
        }
        return false; 
    }

        // ----- 4.b) Esquinas clásicas (para Strict / Majority) -----
        private bool CornersSupport(int requiredHits)
        {
            int hits = 0;
            Vector3 behind = testTransform.TransformVector(new Vector3(0f, 0f, -FaceOffset));
            Vector3 fwd    = testTransform.TransformVector(new Vector3(0f, 0f,  ForwardProbe));

            Vector3 basePos = testTransform.position;

            Vector3[] cornerLocal = new[]
            {
                new Vector3(-HalfWidth, -HalfHeight, -FaceOffset),
                new Vector3(-HalfWidth,  HalfHeight, -FaceOffset),
                new Vector3( HalfWidth, -HalfHeight, -FaceOffset),
                new Vector3( HalfWidth,  HalfHeight, -FaceOffset),
            };

            for (int i = 0; i < cornerLocal.Length; ++i)
            {
                Vector3 p0 = basePos + testTransform.TransformVector(cornerLocal[i]);
                if (Physics.Linecast(p0, p0 + fwd, placementMask)) hits++;
            }
            return hits >= requiredHits;
        }

        // ----- 4.c) Cobertura por muestreo de perímetro + SphereCast -----
        private bool HasSufficientSupport()
        {
            int hits = 0;
            float r = CornerProbeRadius;
            Vector3 fwd = testTransform.TransformVector(new Vector3(0f, 0f, ForwardProbe));

            for (int i = 0; i < supportSamples; ++i)
            {
                float t = i / (float)supportSamples;
                Vector3 p0 = PerimeterPointWorld(t, -FaceOffset); // punto en perímetro, detrás del plano
                if (Physics.SphereCast(p0, r, fwd.normalized, out _, fwd.magnitude, placementMask))
                    hits++;
            }
            float coverage = hits / (float)supportSamples;
            return coverage >= supportCoverageMin;
        }

        // ----- 4.d) Curvatura local (opcional). Devuelve true si no quieres usarla. -----
        private bool LocalCurvatureOK()
        {
            if (curvatureToleranceDeg <= 0f) return true;

            // normal en centro
            Vector3 n0;
            if (!Physics.Raycast(testTransform.position + testTransform.forward * Physics.defaultContactOffset,
                                 -testTransform.forward, out var hit0, FaceOffset + Physics.defaultContactOffset, placementMask))
                return true; // si no hay lectura fiable, no bloqueamos
            n0 = hit0.normal;

            int k = Mathf.Max(16, supportSamples / 3);
            float worst = 0f;

            for (int i = 0; i < k; ++i)
            {
                Vector3 pw = PerimeterPointWorld(i / (float)k, 0f);
                if (Physics.Raycast(pw + testTransform.forward * Physics.defaultContactOffset,
                                    -testTransform.forward, out var hi, FaceOffset + Physics.defaultContactOffset, placementMask))
                {
                    float ang = Vector3.Angle(n0, hi.normal);
                    if (ang > worst) worst = ang;
                    if (worst > curvatureToleranceDeg) return false;
                }
            }
            return true;
        }

        // Punto en el perímetro del rectángulo del portal en espacio MUNDO
        private Vector3 PerimeterPointWorld(float t01, float zLocal)
        {
            t01 = Mathf.Repeat(t01, 1f);
            float w = HalfWidth;
            float h = HalfHeight;

            Vector3 local;
            if (t01 < 0.25f)
            {   // borde inferior: x[-w..w], y=-h
                float u = Mathf.InverseLerp(0f, 0.25f, t01);
                local = new Vector3(Mathf.Lerp(-w, w, u), -h, zLocal);
            }
            else if (t01 < 0.5f)
            {   // borde derecho: x=+w, y[-h..h]
                float u = Mathf.InverseLerp(0.25f, 0.5f, t01);
                local = new Vector3(+w, Mathf.Lerp(-h, h, u), zLocal);
            }
            else if (t01 < 0.75f)
            {   // borde superior: x[w..-w], y=+h
                float u = Mathf.InverseLerp(0.5f, 0.75f, t01);
                local = new Vector3(Mathf.Lerp(w, -w, u), +h, zLocal);
            }
            else
            {   // borde izquierdo: x=-w, y[h..-h]
                float u = Mathf.InverseLerp(0.75f, 1f, t01);
                local = new Vector3(-w, Mathf.Lerp(h, -h, u), zLocal);
            }
            return testTransform.TransformPoint(local);
        }
    
    public bool TryComputePlacement(
        Collider wallCollider, Vector3 pos, Quaternion rot,
        out Vector3 adjustedPos, out Quaternion adjustedRot)
    {
        // 1) Pre-posicionado exacto como en PlacePortal
        testTransform.position = pos;
        testTransform.rotation = rot;
        testTransform.position -= testTransform.forward * 0.001f;

        // 2) Misma tubería de correcciones
        FixOverhangs();
        FixIntersects();

        // 3) Validación final
        bool ok = CheckOverlap();

        // 4) Resultado para el preview (sin efectos colaterales)
        adjustedPos = testTransform.position;
        adjustedRot = testTransform.rotation;
        return ok;
    }
    private void NudgeForSupport()
    {
        if (edgePolicy != EdgePolicy.Coverage) return; // solo con cobertura
        if (HasSufficientSupport()) return;

        Vector3 r = testTransform.right * Mathf.Max(Physics.defaultContactOffset, 0.02f * HalfWidth);
        Vector3 u = testTransform.up    * Mathf.Max(Physics.defaultContactOffset, 0.02f * HalfHeight);

        float best = 0f; Vector3 bestStep = Vector3.zero;

        foreach (var step in new[] { r, -r, u, -u })
        {
            testTransform.position += step;
            float score = SupportScore();
            if (score > best) { best = score; bestStep = step; }
            testTransform.position -= step;
        }
        if (best > 0f) testTransform.position += bestStep;
    }

    private float SupportScore()
    {
        int hits = 0;
        float r = CornerProbeRadius;
        Vector3 fwd = testTransform.TransformVector(new Vector3(0f, 0f, ForwardProbe));
        for (int i = 0; i < supportSamples; ++i)
        {
            float t = i / (float)supportSamples;
            Vector3 p0 = PerimeterPointWorld(t, -FaceOffset);
            if (Physics.SphereCast(p0, r, fwd.normalized, out _, fwd.magnitude, placementMask)) hits++;
        }
        return hits / (float)supportSamples;
    }

    public void RemovePortal()
    {
        gameObject.SetActive(false);
        IsPlaced = false;
    }
}