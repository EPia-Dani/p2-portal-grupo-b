using System.Collections;
using System.Collections.Generic;
using _Scripts.Portals;
using UnityEngine;
using UnityEngine.VFX;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class Portal : MonoBehaviour
{
    [Header("Link")]
    [SerializeField] private Portal otherPortal;

    [Header("Visuals")]
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private Transform particleSpawn;
    private VisualEffect portalParticles;
    [SerializeField] private Color portalColor = Color.cyan;
    private Material mat;
    
    public Transform ScreenTransform => screenRenderer != null ? screenRenderer.transform : null;

    [Header("Colliders")]
    [SerializeField] private Collider wallCollider;       // the surface proxy, if any
    [SerializeField] private GameObject portalCollider;   // trigger in the opening

    [Header("Masks")]
    [SerializeField] private LayerMask placementMask; // valid surfaces and both portal layers
    
    LayerMask EffectiveMask
    {
        get
        {
            int mask = placementMask;
            int selfLayer = gameObject.layer;
            mask &= ~(1 << selfLayer);   // clear own layer bit
            return mask;
        }
    }

    [Header("Placement")]
    [SerializeField, Range(0.02f, 0.2f)] private float supportDepthFactor = 0.08f;
    [SerializeField, Range(0.00f, 0.20f)] private float edgeExtraFactor = 0.05f;
    [SerializeField, Range(0.001f, 0.10f)] private float overlapThickness = 0.02f;
    [SerializeField, Range(0.02f, 0.20f)] private float forwardProbeFactor = 0.10f;
    [SerializeField, Range(0.001f, 0.05f)]
    private float wallGap = 0.02f;
    // Snap-to-nearest-valid search (distance in meters on the wall plane)
    [SerializeField, Range(0.02f, 0.50f)] private float snapStep = 0.5f;  // ring spacing
    [SerializeField, Range(1, 24)]        private int snapRings = 8;       // how many rings
    [SerializeField, Range(8, 64)]        private int snapAngles = 24;     // samples per ring

    
    [Header("Sound")]
    [SerializeField] private AudioClip placeSound;
    [SerializeField] private AudioClip enterSound;
    [SerializeField] private AudioClip exitSound;

    public enum EdgePolicy { Strict4of4, Majority3of4, Coverage }
    [SerializeField] private EdgePolicy edgePolicy = EdgePolicy.Coverage;
    [SerializeField, Range(16,128)] private int supportSamples = 64;
    [SerializeField, Range(0.5f,1f)] private float supportCoverageMin = 0.85f;

    private BoxCollider boxCol;
    private Transform testT;
    
    private readonly List<PortalableBase> _portalables = new();
    
    [SerializeField, Range(0.5f, 2f)] private float desiredScale = 1f;
    public float DesiredScale => desiredScale;
    public float CurrentScale => (ScreenTransform ? ScreenTransform.lossyScale.x : transform.lossyScale.x);
    public void SetDesiredScale(float s) => desiredScale = Mathf.Clamp(s, 0.5f, 2f);


    public bool IsPlaced { get; private set; }
    public Portal OtherPortal => otherPortal;
    public Renderer Renderer => screenRenderer;
    public Color PortalColor => portalColor;
    public Collider WallCollider => wallCollider;
    public GameObject PortalCollider => portalCollider;

// replace the existing HalfWidth/HalfHeight props
    float ScaleForChecks => IsPlaced ? transform.lossyScale.x : desiredScale;
    float HalfWidth  => 0.5f * boxCol.size.x * ScaleForChecks;
    float HalfHeight => 0.5f * boxCol.size.y * ScaleForChecks;

    float EdgeExtra   => Mathf.Max(Physics.defaultContactOffset, edgeExtraFactor * Mathf.Min(HalfWidth, HalfHeight));
    float FaceOffset  => Mathf.Max(Physics.defaultContactOffset, supportDepthFactor * HalfHeight);
    float ForwardProbe => Mathf.Max(Physics.defaultContactOffset, forwardProbeFactor * HalfHeight);
    
    Transform InPlane  => ScreenTransform ? ScreenTransform : transform;
    Transform OutPlane => OtherPortal && OtherPortal.ScreenTransform 
        ? OtherPortal.ScreenTransform 
        : OtherPortal.transform;

    float InScale()  => InPlane.lossyScale.x;
    float OutScale() => OutPlane.lossyScale.x;

    public void GetHalfExtentsForScale(float scale, out float halfW, out float halfH)
    {
        var size = boxCol.size;
        halfW = 0.5f * size.x * scale;
        halfH = 0.5f * size.y * scale;
    }

    void Awake()
    {
        boxCol = GetComponent<BoxCollider>();
        boxCol.isTrigger = true;

        if (screenRenderer == null) screenRenderer = GetComponentInChildren<Renderer>(true);
        
        mat = screenRenderer.material;
        mat.SetColor("_FallbackColor", portalColor);

        var go = new GameObject(name + "_TestT");
        go.hideFlags = HideFlags.HideAndDontSave;
        testT = go.transform;
    }

    void OnDestroy()
    {
        if (testT != null) Destroy(testT.gameObject);
    }

    void Update()
    {
        if (!IsPlaced) return;
        
        foreach (var p in _portalables)
        {
            if (p == null) continue;
            if (p.HasCrossedPlane(this) && OtherPortal != null)
            {
                PlayEnterSound();
                p.Warp();
                OtherPortal.PlayExitSound();
            }

        }
    }
    
    public void PlayPlaceSound()
    {
        if (placeSound != null)
            AudioSource.PlayClipAtPoint(placeSound, ScreenTransform.position);
    }
    
    public void PlayEnterSound() 
    {
        if (enterSound != null)
            AudioSource.PlayClipAtPoint(enterSound, ScreenTransform.position);
    }
    
    public void PlayExitSound() 
    {
        if (exitSound != null)
            AudioSource.PlayClipAtPoint(exitSound, ScreenTransform.position);
    }
    
    private void OnTriggerEnter(Collider other) 
    {
        var portalable = other.GetComponentInParent<PortalableBase>();
        if (portalable != null && !_portalables.Contains(portalable))
        {
            _portalables.Add(portalable);
        }
        portalable.SetIsInPortal(this, otherPortal);
    }
    
    private void OnTriggerExit(Collider other) 
    {
        var portalable = other.GetComponentInParent<PortalableBase>();
        if (portalable != null && _portalables.Contains(portalable))
        {
            _portalables.Remove(portalable);
        }
        portalable.ExitPortal();
    }


    /// <summary>
    /// Attempts to compute a valid portal placement on the given surface at the given hit point and
    /// initial rotation. If successful, returns true and outputs the final position and rotation.
    /// If not successful, returns false.
    /// </summary>
    public bool TryComputePlacement(Collider surface, Vector3 hitPoint, Quaternion initialRotation, float scale,
        out Vector3 finalPosition, out Quaternion finalRotation)
    {
        finalPosition = default;
        finalRotation = default;
        if (surface == null) return false;

        var disabled = new List<Collider>();
        void Disable(Collider c)
        {
            if (c != null && c.enabled)
            {
                c.enabled = false;
                disabled.Add(c);
            }
        }
        if (portalCollider != null)
        {
            var c = portalCollider.GetComponents<Collider>();
            foreach (var col in c)
                Disable(col);
        }

        try
        {
            // basis from rotation
            Vector3 fwd   = -(initialRotation * Vector3.forward); // into wall
            Vector3 right =  (initialRotation * Vector3.right);
            Vector3 up    =  (initialRotation * Vector3.up);
            right = Vector3.Normalize(Vector3.ProjectOnPlane(right, fwd));
            up    = Vector3.Normalize(Vector3.Cross(fwd, right));

            // use scale explicitly for radius
            GetHalfExtentsForScale(scale, out float halfW, out float halfH);
            float maxRadius = Mathf.Sqrt(halfW * halfW + halfH * halfH) + EdgeExtra;

            // 1) raw hit
            if (EvaluateAt(hitPoint, initialRotation, out finalPosition, out finalRotation))
                return true;

            // 2) snap search: early–exit on first success
            for (int ring = 1; ring <= snapRings; ++ring)
            {
                float r = Mathf.Min(ring * snapStep, maxRadius);
                for (int i = 0; i < snapAngles; ++i)
                {
                    float theta = (2f * Mathf.PI) * (i / (float)snapAngles);
                    Vector3 offset = right * (r * Mathf.Cos(theta)) + up * (r * Mathf.Sin(theta));
                    Vector3 candidate = hitPoint + offset;

                    if (EvaluateAt(candidate, initialRotation, out finalPosition, out finalRotation))
                        return true;
                }
            }
            return false;
        }
        finally
        {
            foreach (var c in disabled)
                c.enabled = true;
        }
    }

    // Runs all checks + fixes at a given wall point, returns the committed pose if valid.
    bool EvaluateAt(Vector3 centerWS, Quaternion rotWS, out Vector3 finalPosition, out Quaternion finalRotation)
    {
        finalPosition = default; 
        finalRotation = default;

        testT.SetPositionAndRotation(centerWS, rotWS);

        if (!HasFrontSurface()) return false;
        FixOverhangs();
        FixIntersections();
        if (!CheckSupportPolicy()) return false;
        
        const float kVerticalSnapDeg = 5f; // tolerance: treat ~floor as floor
        Vector3 f = testT.forward;         // still points into the surface
        float angToUp = Vector3.Angle(f, Vector3.up);
        
        if (angToUp > kVerticalSnapDeg && angToUp < 175f)
        {
            testT.rotation = Quaternion.LookRotation(f, Vector3.up);
        }

        finalPosition = testT.position - testT.forward * wallGap; // forward points into wall
        finalRotation = testT.rotation;
        return true;
    }

    // ==== Checks ====
    bool HasFrontSurface()
    {
        Vector3 origin = testT.position - testT.forward * FaceOffset;
        return Physics.Raycast(origin, testT.forward, ForwardProbe, EffectiveMask, QueryTriggerInteraction.Ignore);
    }

    void FixOverhangs()
    {
        float w = HalfWidth;
        float h = HalfHeight;
        float clampMoveW = w + EdgeExtra;
        float clampMoveH = h + EdgeExtra;

        NudgeEdge(testT.right, +1f, clampMoveW);
        NudgeEdge(testT.right, -1f, clampMoveW);
        NudgeEdge(testT.up,    +1f, clampMoveH);
        NudgeEdge(testT.up,    -1f, clampMoveH);

        void NudgeEdge(Vector3 axis, float sign, float clampMove)
        {
            float extent = axis == testT.right ? w : h;
            Vector3 rim = axis * (sign > 0 ? +extent : -extent);
            Vector3 start = testT.position + rim - testT.forward * FaceOffset;
            float maxDist = clampMove * 2f;

            if (Physics.SphereCast(start, EdgeExtra, testT.forward, out var hit, maxDist, EffectiveMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 delta = hit.point - (start + testT.forward * EdgeExtra);
                float move = Mathf.Min(delta.magnitude, clampMove);
                testT.position += testT.forward * move;
            }
        }
    }

    // Portal.cs — FixIntersections(): change the sign when resolving overlaps
    void FixIntersections()
    {
        float r = Mathf.Min(HalfWidth, HalfHeight);
        float slab = Mathf.Max(Physics.defaultContactOffset, 0.5f * overlapThickness);

        Vector3 a = testT.position - testT.up * (HalfHeight - r) - testT.forward * slab;
        Vector3 b = testT.position + testT.up * (HalfHeight - r) - testT.forward * slab;

        var cols = Physics.OverlapCapsule(a, b, r, EffectiveMask, QueryTriggerInteraction.Ignore);
        if (cols.Length == 0) return;

        // Pull OUT of the wall, not into it
        testT.position -= testT.forward * (slab * 1.5f);
    }


    bool CheckSupportPolicy()
    {
        switch (edgePolicy)
        {
            case EdgePolicy.Strict4of4:   return CardinalSupport(4);
            case EdgePolicy.Majority3of4: return CardinalSupport(3);
            default:                      return HasSufficientSupport();
        }
    }

    bool HasSufficientSupport()
    {
        int hits = 0;
        int samples = Mathf.Max(16, supportSamples);
        float rProbe = Mathf.Max(Physics.defaultContactOffset * 1.5f, EdgeExtra * 0.5f);

        for (int i = 0; i < samples; ++i)
        {
            float t = (i + 0.5f) / samples;
            Vector3 p0 = EllipsePerimeterPointWorld(t, -FaceOffset);
            if (Physics.SphereCast(p0, rProbe, testT.forward, out _, ForwardProbe, EffectiveMask, QueryTriggerInteraction.Ignore))
                hits++;
        }
        float cov = hits / (float)samples;
        return cov >= supportCoverageMin;
    }

    bool CardinalSupport(int minCount)
    {
        int ok = 0;
        float rProbe = Mathf.Max(Physics.defaultContactOffset * 1.5f, EdgeExtra * 0.5f);

        Vector3[] local =
        {
            new (-HalfWidth, 0f, -FaceOffset),
            new (+HalfWidth, 0f, -FaceOffset),
            new (0f, -HalfHeight, -FaceOffset),
            new (0f, +HalfHeight, -FaceOffset),
        };

        foreach (var lp in local)
        {
            Vector3 p0 = testT.TransformPoint(lp);
            if (Physics.SphereCast(p0, rProbe, testT.forward, out _, ForwardProbe, EffectiveMask, QueryTriggerInteraction.Ignore))
                ok++;
        }
        return ok >= minCount;
    }

    Vector3 EllipsePerimeterPointWorld(float t01, float zLocal)
    {
        float theta = 2f * Mathf.PI * Mathf.Repeat(t01, 1f);
        float x = HalfWidth * Mathf.Cos(theta);
        float y = HalfHeight * Mathf.Sin(theta);
        return testT.TransformPoint(new Vector3(x, y, zLocal));
    }
    
    public void PlacePrecomputed(Collider surface, Vector3 finalPosition, Quaternion finalRotation)
    {
        if (surface == null) return;
        wallCollider = surface;
        if (portalParticles != null)
            portalParticles.transform.parent = null;
        transform.SetPositionAndRotation(finalPosition, finalRotation);
        transform.localScale = new Vector3(desiredScale, desiredScale, desiredScale);
        IsPlaced = true;
        if (portalCollider) portalCollider.SetActive(true);
        PlayPlaceSound();
        StartCoroutine(OpenPortal(0.5f));
        if (OtherPortal.IsPlaced)
            StartCoroutine(OtherPortal.OpenPortal(0.5f));
    }
    
    public void GetHalfExtentsForPreview(out float halfW, out float halfH)
    {
        halfW = 0.5f * boxCol.size.x * desiredScale;
        halfH = 0.5f * boxCol.size.y * desiredScale;
    }
    
    public Vector3 MapPointToOther(Vector3 pointWS, Vector3 inDirWS, 
        float enterOffset = 0.0f, float exitBackoff = 0.06f)
    {
        if (OtherPortal == null) return pointWS;

        var inT  = InPlane;
        var outT = OutPlane;

        // slight push-through to avoid mapping exactly on the plane
        Vector3 pInWS = pointWS + inDirWS.normalized * enterOffset;

        // compose M_out * S_xy(r) * HalfTurn * M_in^-1
        float r = OutScale() / InScale();
        Matrix4x4 M_in_inv = inT.worldToLocalMatrix;
        Matrix4x4 HalfTurn = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));
        Matrix4x4 Sxy      = Matrix4x4.Scale(new Vector3(r, r, 1f));
        Matrix4x4 M_out    = outT.localToWorldMatrix;

        Vector3 pLocalIn  = M_in_inv.MultiplyPoint3x4(pInWS);
        Vector3 pLocalOut = (Sxy * HalfTurn).MultiplyPoint3x4(pLocalIn);
        Vector3 mapped    = M_out.MultiplyPoint3x4(pLocalOut);

        // back off a hair to avoid immediate re-hit
        return mapped - outT.forward * exitBackoff;
    }


    public Vector3 MapDirectionToOther(Vector3 dirWS)
    {
        if (OtherPortal == null) return dirWS.normalized;

        var inT  = InPlane;
        var outT = OutPlane;

        Matrix4x4 M_in_inv = inT.worldToLocalMatrix;
        Matrix4x4 HalfTurn = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));
        Matrix4x4 M_out    = outT.localToWorldMatrix;

        Vector3 dLocalIn  = M_in_inv.MultiplyVector(dirWS).normalized;
        Vector3 dLocalOut = HalfTurn.MultiplyVector(dLocalIn);
        Vector3 mapped    = M_out.MultiplyVector(dLocalOut).normalized;
        return mapped;
    }

    
    IEnumerator OpenPortal(float dur, float linkLagNorm = 0.15f)
    {
        // particles (unchanged)
        portalParticles?.SendEvent("Deactivate");
        Destroy(portalParticles?.gameObject, 2f);
        portalParticles = null;

        // props reset
        mat.SetFloat("_LinkedPortalValid", 0f);
        mat.SetFloat("_Link", 0f);
        mat.SetFloat("_LinkAmount", 0f); // if older material still reads this

        if (portalParticles == null && particlePrefab != null)
        {
            var go = Instantiate(particlePrefab, particleSpawn);
            portalParticles = go.GetComponent<VisualEffect>();
        }
        portalParticles?.SendEvent("Activate");

        // decide link state once at start
        bool hasLink = (OtherPortal != null && OtherPortal.IsPlaced);
        if (hasLink) mat.SetFloat("_LinkedPortalValid", 1f);

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float x = Mathf.Clamp01(t / dur);

            // ease-out curve
            float Ease(float u) => 1f - Mathf.Pow(1f - u, 3f);

            // outer opening
            float open = Ease(x);
            mat.SetFloat("_Open", open);

            // link aperture lags behind by linkLagNorm of the timeline
            float xLink = Mathf.Clamp01((x - linkLagNorm) / (1f - linkLagNorm));
            float link = hasLink ? Ease(xLink) : 0f;

            // write both property names for compatibility
            mat.SetFloat("_Link", link);
            mat.SetFloat("_LinkAmount", link);

            yield return null;
        }

        // final clamp
        mat.SetFloat("_Open", 1f);
        if (hasLink)
        {
            mat.SetFloat("_LinkedPortalValid", 1f);
            mat.SetFloat("_Link", 1f);
            mat.SetFloat("_LinkAmount", 1f);
        }
        else
        {
            mat.SetFloat("_LinkedPortalValid", 0f);
            mat.SetFloat("_Link", 0f);
            mat.SetFloat("_LinkAmount", 0f);
        }
    }
    
}
