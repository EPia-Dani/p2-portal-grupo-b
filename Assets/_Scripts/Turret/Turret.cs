using _Scripts.Interfaces;
using UnityEngine;
public class Turret : MonoBehaviour, IDamageable {
    [Header("Refs")]
    [SerializeField] Transform headPivot, barrelPivot, muzzle;
    [SerializeField] LayerMask sightMask;      // World | Player | Grabbable
    [SerializeField] Transform target;         // assign Player at runtime
    [Header("Detect")]
    [SerializeField] float wakeRadius = 12f;
    [SerializeField] float fovDeg = 60f;
    [SerializeField] float maxTrackDist = 20f;
    [SerializeField] float losCheckInterval = 0.05f;
    [Header("Aim")]
    [SerializeField] float yawSpeed = 240f;
    [SerializeField] float pitchSpeed = 180f;
    [SerializeField] Vector2 pitchClamp = new(-20, 45);
    [Header("Fire")]
    [SerializeField] float fireRate = 12f;     // rounds/sec
    [SerializeField] float bulletDamage = 5f;
    [SerializeField] float bulletSpreadDeg = 0.6f;
    [SerializeField] float maxFireDist = 30f;
    [SerializeField] LineRenderer tracer;      // optional
    [Header("Stability")]
    [SerializeField] float tipDisableAngle = 40f; // if tipped over
    [SerializeField] float health = 40f;

    enum State { Sleep, Search, Track, Fire, Disabled, Dead }
    State state = State.Sleep;
    float nextFire, losTimer;

    void Start(){
        if (!target) target = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Update(){
        if (state == State.Dead) return;
        if (IsTipped()){ SetState(State.Disabled); return; }

        switch(state){
            case State.Sleep:
                if (InWakeRange()) SetState(State.Search);
                break;
            case State.Search:
                if (CanSeeTarget()) SetState(State.Track);
                else if (!InWakeRange()) SetState(State.Sleep);
                break;
            case State.Track:
                AimAtTarget();
                if (!InSightCone() || !HasLOS()) { SetState(State.Search); break; }
                if (InFireRange()) SetState(State.Fire);
                break;
            case State.Fire:
                AimAtTarget();
                if (!InSightCone() || !HasLOS() || !InFireRange()) { SetState(State.Track); break; }
                TryFire();
                break;
            case State.Disabled:
                // stays down until righted or destroyed
                break;
        }
    }

    bool InWakeRange(){
        if (!target) return false;
        return Vector3.SqrMagnitude(target.position - transform.position) <= wakeRadius*wakeRadius;
    }

    bool InSightCone(){
        if (!target) return false;
        Vector3 to = (target.position - headPivot.position);
        float dist = to.magnitude;
        if (dist > maxTrackDist) return false;
        to /= dist;
        return Vector3.Angle(headPivot.forward, to) <= fovDeg*0.5f;
    }

    bool CanSeeTarget(){
        return InSightCone() && HasLOS();
    }

    bool HasLOS(){
        losTimer -= Time.deltaTime;
        if (losTimer > 0f) return true; // use last result
        losTimer = losCheckInterval;
        if (!target) return false;
        var origin = barrelPivot.position;
        var dir = (target.position + Vector3.up*0.9f - origin).normalized;
        if (Physics.Raycast(origin, dir, out var hit, maxTrackDist, sightMask, QueryTriggerInteraction.Ignore)){
            return hit.collider.CompareTag("Player");
        }
        return false;
    }

    void AimAtTarget(){
        if (!target) return;
        Vector3 localDir = headPivot.InverseTransformDirection((target.position + Vector3.up*0.9f - headPivot.position).normalized);
        // Yaw
        float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        headPivot.localRotation = Quaternion.RotateTowards(headPivot.localRotation, Quaternion.Euler(0, yaw, 0), yawSpeed*Time.deltaTime);
        // Pitch
        Vector3 dirWorld = (target.position + Vector3.up*0.9f - barrelPivot.position).normalized;
        float pitch = -Mathf.Asin(dirWorld.y) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, pitchClamp.x, pitchClamp.y);
        var targetPitch = Quaternion.Euler(pitch, 0, 0);
        barrelPivot.localRotation = Quaternion.RotateTowards(barrelPivot.localRotation, targetPitch, pitchSpeed*Time.deltaTime);
    }

    bool InFireRange(){
        return Vector3.Distance(target.position, muzzle.position) <= maxFireDist;
    }

    void TryFire(){
        if (Time.time < nextFire) return;
        nextFire = Time.time + 1f/fireRate;

        // spread
        var dir = muzzle.forward;
        dir = Quaternion.AngleAxis(Random.Range(-bulletSpreadDeg, bulletSpreadDeg), headPivot.up) * dir;
        dir = Quaternion.AngleAxis(Random.Range(-bulletSpreadDeg, bulletSpreadDeg), barrelPivot.right) * dir;

        if (Physics.Raycast(muzzle.position, dir, out var hit, maxFireDist, sightMask, QueryTriggerInteraction.Ignore)){
            // tracer (optional)
            if (tracer){
                tracer.enabled = true;
                tracer.positionCount = 2;
                tracer.SetPosition(0, muzzle.position);
                tracer.SetPosition(1, hit.point);
                // quick disable next frame
                Invoke(nameof(HideTracer), 0.03f);
            }

            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null) dmg.ApplyDamage(bulletDamage, hit.point, hit.normal);
            // add your impact VFX/decal here
        }
    }
    void HideTracer(){ if (tracer) tracer.enabled = false; }

    bool IsTipped(){
        // if turret up deviates a lot from world up, it falls/gets disabled
        float angle = Vector3.Angle(transform.up, Vector3.up);
        return angle > tipDisableAngle;
    }

    public void ApplyDamage(float dmg, Vector3 p, Vector3 n){
        if (state == State.Dead) return;
        health -= dmg;
        if (health <= 0f){ Die(); }
        else if (state == State.Sleep) SetState(State.Search);
    }

    void SetState(State s){
        if (state == s) return;
        state = s;
        switch(s){
            case State.Sleep: tracer?.gameObject.SetActive(false); break;
            case State.Disabled: tracer?.gameObject.SetActive(false); break;
        }
    }

    void Die(){
        state = State.Dead;
        tracer?.gameObject.SetActive(false);
        // play explode VFX/SFX, disable colliders
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        enabled = false;
    }
}
