using System.Collections.Generic;
using _Scripts.Interfaces;
using UnityEngine;

public class LaserEmitterSegmented : MonoBehaviour
{
    [Header("Beam")]
    [SerializeField] float maxDistance = 200f;
    [SerializeField] int   maxBounces  = 16;
    [SerializeField] int   maxPortalHops = 8;
    [SerializeField] LayerMask laserMask = ~0;
    [SerializeField] LayerMask portalScreenMask = 0;
    [SerializeField] float surfaceOffset = 0.01f;

    [Header("Visuals")]
    [SerializeField] LineRenderer segmentTemplate;   // assign a prefab or the LR on this object
    [SerializeField] ParticleSystem hitSparksPrefab;
    [SerializeField] bool useWorldSpace = true;

    readonly List<LineRenderer> _pool = new();
    int _activeSegments;
    ParticleSystem _hitSparks;

    void Awake()
    {
        if (!segmentTemplate)
        {
            segmentTemplate = GetComponent<LineRenderer>();
            if (!segmentTemplate)
                segmentTemplate = gameObject.AddComponent<LineRenderer>();
        }
        segmentTemplate.enabled = false;
        if (hitSparksPrefab)
        {
            _hitSparks = Instantiate(hitSparksPrefab);
            _hitSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void Update() => TraceAndRender();

    void TraceAndRender()
    {
        _activeSegments = 0;
        var segmentPoints = new List<Vector3>(8);

        Vector3 origin = transform.position;
        Vector3 dir = transform.forward.normalized;

        // begin first segment
        BeginSegment(segmentPoints, origin);

        var ray = new Ray(origin, dir);
        bool hasFinalHit = false;
        RaycastHit lastHit = default;

        int bouncesLeft = Mathf.Max(0, maxBounces);
        int hopsLeft    = Mathf.Max(0, maxPortalHops);
        float remaining = maxDistance;
        int combinedMask = laserMask | portalScreenMask;

        while (bouncesLeft >= 0 && hopsLeft >= 0)
        {
            if (!Physics.Raycast(ray, out var hit, remaining, combinedMask, QueryTriggerInteraction.Collide))
            {
                segmentPoints.Add(ray.origin + ray.direction * remaining);
                break;
            }

            // end current segment at the hit point
            segmentPoints.Add(hit.point);
            lastHit = hit; hasFinalHit = true;

            remaining -= hit.distance;

            // Portal hop → end segment here, start a new one from teleported origin
            if (IsOnLayer(hit.collider.gameObject.layer, portalScreenMask))
            {
                if (hopsLeft == 0) break;
                if (!TryThroughPortal(hit, ray.direction, out var newOrigin, out var newDir))
                    break;

                CommitSegment(segmentPoints);           // finish segment to the portal
                BeginSegment(segmentPoints, newOrigin); // start AFTER portal
                ray = new Ray(newOrigin + newDir * surfaceOffset, newDir);
                hopsLeft--;
                continue;
            }

            // Redirector (e.g., RefractionCube)
            if (hit.collider.TryGetComponent<ILaserRedirector>(out var redirector)
                && redirector.TryRedirect(ray, hit, out var outRay))
            {
                redirector.Activate(Time.frameCount);
                CommitSegment(segmentPoints);                     // finish segment to redirector
                BeginSegment(segmentPoints, outRay.origin);       // start new from redirect point
                ray = new Ray(outRay.origin + outRay.direction.normalized * surfaceOffset,
                              outRay.direction.normalized);
                bouncesLeft--;
                continue;
            }

            // Mirror by tag
            if (hit.collider.CompareTag("Mirror"))
            {
                Vector3 r = Vector3.Reflect(ray.direction, hit.normal).normalized;
                CommitSegment(segmentPoints);               // finish at mirror
                BeginSegment(segmentPoints, hit.point);     // start new leaving mirror
                ray = new Ray(hit.point + r * surfaceOffset, r);
                bouncesLeft--;
                continue;
            }
            
            if (hit.collider.TryGetComponent<ILaserReceiver>(out var receiver))
                receiver.LaserHit(hit.point, hit.normal, Time.frameCount);
            
            if (hit.collider.GetComponent<IDamageable>() != null)
            {
                // apply damage
                hit.collider.GetComponent<IDamageable>()?.ApplyDamage(25f * Time.deltaTime, hit.point, hit.normal);
            }
            
            break;
        }

        // finalize last open segment
        CommitSegment(segmentPoints);

        // disable unused pooled segments
        for (int i = _activeSegments; i < _pool.Count; i++)
            _pool[i].enabled = false;

        // sparks at final impact or hide
        if (_hitSparks)
        {
            if (hasFinalHit)
            {
                _hitSparks.transform.SetPositionAndRotation(lastHit.point, Quaternion.LookRotation(lastHit.normal));
                if (!_hitSparks.isPlaying) _hitSparks.Play();
            }
            else if (_hitSparks.isPlaying)
            {
                _hitSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    void BeginSegment(List<Vector3> seg, Vector3 start)
    {
        seg.Clear();
        seg.Add(start);
    }

    void CommitSegment(List<Vector3> seg)
    {
        if (seg.Count < 2) return;

        var lr = GetSegmentLR(_activeSegments++);
        lr.useWorldSpace = useWorldSpace;
        lr.positionCount = seg.Count;

        if (useWorldSpace)
        {
            for (int i = 0; i < seg.Count; i++) lr.SetPosition(i, seg[i]);
        }
        else
        {
            for (int i = 0; i < seg.Count; i++) lr.SetPosition(i, transform.InverseTransformPoint(seg[i]));
        }
    }

    LineRenderer GetSegmentLR(int index)
    {
        while (_pool.Count <= index)
        {
            var go = new GameObject($"LaserSegment_{_pool.Count}");
            go.transform.SetParent(transform, worldPositionStays: true);
            var lr = go.AddComponent<LineRenderer>();
            CopyLR(segmentTemplate, lr);
            _pool.Add(lr);
        }
        var seg = _pool[index];
        seg.enabled = true;
        return seg;
    }

    static void CopyLR(LineRenderer from, LineRenderer to)
    {
        to.material = from.material;
        to.textureMode = from.textureMode;
        to.widthMultiplier = from.widthMultiplier;
        to.widthCurve = from.widthCurve;
        to.numCornerVertices = from.numCornerVertices;
        to.numCapVertices = from.numCapVertices;
        to.generateLightingData = from.generateLightingData;
        to.shadowCastingMode = from.shadowCastingMode;
        to.receiveShadows = from.receiveShadows;
        to.alignment = from.alignment;
        to.colorGradient = from.colorGradient;
    }

    static bool IsOnLayer(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;

    bool TryThroughPortal(RaycastHit hit, Vector3 inDir, out Vector3 newOrigin, out Vector3 newDir)
    {
        var inPortal = hit.collider.GetComponentInParent<Portal>();
        if (!inPortal || !inPortal.OtherPortal)
        {
            newOrigin = hit.point; newDir = inDir; return false;
        }

        // centers + scale-aware mapping handled by Portal
        newOrigin = inPortal.MapPointToOther(hit.point, inDir, enterOffset: surfaceOffset, exitBackoff: 0.06f);
        newDir    = inPortal.MapDirectionToOther(inDir);
        return true;
    }
}
