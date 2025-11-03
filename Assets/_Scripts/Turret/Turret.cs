using _Scripts.Interfaces;
using System.Collections;
using UnityEngine;
public class Turret : MonoBehaviour, IDamageable {
    [Header("Refs")]
    [SerializeField] Transform headPivot, barrelPivot, muzzle;
    [SerializeField] Transform muzzle2; // added second muzzle
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
    [SerializeField] float extraSpreadMultiplier = 8f; // used during panic
    [SerializeField] float maxFireDist = 30f;
    [SerializeField] LineRenderer tracer;      // optional (muzzle 1)
    [SerializeField] LineRenderer tracer2;     // optional (muzzle 2)
    [Header("Recoil")]
    [SerializeField] float recoilForce = 2f; // impulse applied per shot (tweak to taste)
    [SerializeField] bool useForceAtPosition = false; // if true apply AddForceAtPosition, otherwise AddForce at COM
    [Header("Stability")]
    [SerializeField] float tipDisableAngle = 40f; // if tipped over
    [SerializeField] float health = 40f;

    // panic settings
    [SerializeField] float panicDuration = 2f; // seconds to fire randomly after tipping
    [SerializeField] float tipHeightDrop = 0.2f; // if root drops this much from start, consider tipped
    [SerializeField] float tipAngularVelocity = 3f; // if rigidbody angular vel exceeds this, consider tipped

    [Header("Visual")]
    [Tooltip("Renderer that contains an emissive material. If assigned, the material will be instanced and controlled by the turret.")]
    [SerializeField] Renderer emissiveRenderer;
    [Tooltip("Optional material to assign to the renderer on Start (instanced). If left empty the renderer's material will be used.")]
    [SerializeField] Material emissionMaterial;
    [SerializeField] Color emissionColor = Color.red;
    [SerializeField] float emissionIntensity = 1f;
    [SerializeField] float sleepFadeDuration = 1f;

    // runtime emission instance
    Material _matInstance;
    float _currentEmission = 1f; // 0..1
    Coroutine _emissionCoroutine;

    enum State { Sleep, Search, Track, Fire, Disabled, Dead }
    State state = State.Sleep;
    float nextFire, losTimer;

    Coroutine panicCoroutine;

    Rigidbody _rb;
    Vector3 _startUp;
    float _startY;
    [Header("Debug")]
    [SerializeField] bool debugLogs = false;
    // once the turret trips, it should panic once and then disable itself
    bool _tripped = false;

    void Start(){
        if (!target) target = GameObject.FindGameObjectWithTag("Player")?.transform;
        _rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();
        _startUp = transform.up;
        _startY = transform.position.y;
        if (tracer != null) tracer.enabled = false;
        if (muzzle2 != null && tracer2 != null) tracer2.enabled = false;

        // instantiate the emission material if assigned
        if (emissiveRenderer != null){
            if (emissionMaterial != null){
                _matInstance = Instantiate(emissionMaterial);
                emissiveRenderer.material = _matInstance;
            } else {
                // fallback to renderer's shared material
                _matInstance = emissiveRenderer.material;
            }
            // ensure emission keyword is enabled so emissive color is visible
            _matInstance.EnableKeyword("_EMISSION");
            // set initial emission color/intensity
            _matInstance.SetColor("_EmissionColor", emissionColor * emissionIntensity);
            _currentEmission = emissionIntensity;
        }
    }

    void Update(){
        if (state == State.Dead) return;
        if (_tripped) return; // once tripped, stop regular Update behaviour
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
        // consider both muzzles if available, otherwise fallback to single muzzle
        if (muzzle2 == null) return Vector3.Distance(target.position, muzzle.position) <= maxFireDist;
        float d1 = Vector3.Distance(target.position, muzzle.position);
        float d2 = Vector3.Distance(target.position, muzzle2.position);
        return Mathf.Min(d1, d2) <= maxFireDist;
    }

    void TryFire(){
        if (Time.time < nextFire) return;
        nextFire = Time.time + 1f/fireRate;

        // Fire from muzzle 1
        FireFromMuzzle(muzzle, tracer, bulletSpreadDeg);
        // If second muzzle exists, fire from it as well (both fire once each per cadence)
        if (muzzle2 != null){
            FireFromMuzzle(muzzle2, tracer2, bulletSpreadDeg);
        }
    }

    // Fires a single ray from the given muzzle with given spread in degrees
    void FireFromMuzzle(Transform muzz, LineRenderer tr, float spreadDeg){
        if (muzz == null) return;
        Vector3 dir;
        if (target != null){
            dir = Vector3.Normalize(target.position - muzz.position);
        } else {
            dir = muzz.forward;
        }
        // apply random spread
        dir = Quaternion.AngleAxis(Random.Range(-spreadDeg, spreadDeg), headPivot.up) * dir;
        dir = Quaternion.AngleAxis(Random.Range(-spreadDeg, spreadDeg), barrelPivot.right) * dir;

        // apply recoil: impulse opposite to shot direction
        if (_rb != null && !_rb.isKinematic){
            // use the muzzle's forward as the canonical shot direction for recoil so the turret is pushed backward
            Vector3 recoilDir = -muzz.forward;
            if (useForceAtPosition) _rb.AddForceAtPosition(recoilDir.normalized * recoilForce, muzz.position, ForceMode.Impulse);
            else _rb.AddForce(recoilDir.normalized * recoilForce, ForceMode.Impulse);
            if (debugLogs) Debug.Log($"Applying recoil: dir={recoilDir.normalized}, force={recoilForce}", this);
        }

        if (Physics.Raycast(muzz.position, dir, out var hit, maxFireDist, sightMask, QueryTriggerInteraction.Ignore)){
            if (tr){
                ShowTracer(tr, muzz.position, hit.point);
            }

            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null) dmg.ApplyDamage(bulletDamage, hit.point, hit.normal);
        } else {
            // still show tracer to max distance
            if (tr){
                ShowTracer(tr, muzz.position, muzz.position + dir * maxFireDist);
            }
        }
    }

    void ShowTracer(LineRenderer tr, Vector3 start, Vector3 end){
        if (!tr) return;
        tr.enabled = true;
        tr.positionCount = 2;
        tr.SetPosition(0, start);
        tr.SetPosition(1, end);
        // hide after short time
        StartCoroutine(HideTracerAfter(tr, 0.03f));
    }

    IEnumerator HideTracerAfter(LineRenderer tr, float delay){
        yield return new WaitForSeconds(delay);
        if (tr) tr.enabled = false;
    }

    bool IsTipped(){
        // if turret up deviates a lot from world up, it falls/gets disabled
        // broaden the check to include head/barrel orientation in case the root transform isn't the one tipping
        float baseAngle = Vector3.Angle(transform.up, _startUp);
        float headAngle = headPivot ? Vector3.Angle(headPivot.up, Vector3.up) : 0f;
        float barrelAngle = barrelPivot ? Vector3.Angle(barrelPivot.up, Vector3.up) : 0f;
        // if any significant part is tipped beyond threshold, consider it tipped
        if (baseAngle > tipDisableAngle || headAngle > tipDisableAngle * 1.1f || barrelAngle > tipDisableAngle * 1.1f) {
            if (debugLogs) Debug.Log($"Turret tipping detected by angle: base={baseAngle:F1}, head={headAngle:F1}, barrel={barrelAngle:F1}", this);
            return true;
        }
        // also if the root has dropped significantly from its start height (fell over)
        if (transform.position.y < _startY - tipHeightDrop){
            if (debugLogs) Debug.Log($"Turret tipping detected by drop: startY={_startY:F2}, now={transform.position.y:F2}", this);
            return true;
        }
        // or if the rigidbody is spinning violently
        if (_rb != null && _rb.angularVelocity.sqrMagnitude > tipAngularVelocity * tipAngularVelocity){
            if (debugLogs) Debug.Log($"Turret tipping detected by angular velocity: { _rb.angularVelocity.magnitude:F2}", this);
            return true;
        }
        return false;
    }

    public void ApplyDamage(float dmg, Vector3 p, Vector3 n){
        if (state == State.Dead) return;
        health -= dmg;
        if (health <= 0f){ Die(); }
        else if (state == State.Sleep) SetState(State.Search);
    }

    void SetState(State s){
        if (state == s) return;

        // exiting Disabled should stop panic
        if (state == State.Disabled && panicCoroutine != null){
            StopCoroutine(panicCoroutine);
            panicCoroutine = null;
        }

        if (debugLogs) Debug.Log($"Turret state {state} -> {s}", this);
        state = s;
        switch(s){
            case State.Sleep:
                if (tracer != null) tracer.enabled = false;
                if (tracer2 != null) tracer2.enabled = false;
                // fade out emission if applicable
                if (_matInstance != null && sleepFadeDuration > 0f){
                    if (_emissionCoroutine != null) StopCoroutine(_emissionCoroutine);
                    _emissionCoroutine = StartCoroutine(FadeEmission(0f, sleepFadeDuration));
                }
                break;
            case State.Disabled:
                // ensure we only trip once
                if (_tripped) break;
                _tripped = true;
                // start panic firing for a short duration when tipped
                if (tracer != null) tracer.enabled = false;
                if (tracer2 != null) tracer2.enabled = false;
                if (panicCoroutine != null) StopCoroutine(panicCoroutine);
                if (debugLogs) Debug.Log("Turret entering Disabled: starting panic fire", this);
                panicCoroutine = StartCoroutine(PanicFireRoutine());
                break;
        }

        // If we're not sleeping and we have an emissive material, fade emission back up to the configured intensity
        if (state != State.Sleep && _matInstance != null){
            float fadeDur = Mathf.Max(0.05f, sleepFadeDuration);
            if (_emissionCoroutine != null) StopCoroutine(_emissionCoroutine);
            _emissionCoroutine = StartCoroutine(FadeEmission(emissionIntensity, fadeDur));
        }
    }

    IEnumerator FadeEmission(float targetIntensity, float duration){
        float startIntensity = _currentEmission;
        float t = 0f;
        while (t < duration){
            t += Time.deltaTime;
            float lerpT = Mathf.Clamp01(t / duration);
            // smoothstep for easing
            lerpT = lerpT * lerpT * (3f - 2f * lerpT);
            _currentEmission = Mathf.Lerp(startIntensity, targetIntensity, lerpT);
            _matInstance.SetColor("_EmissionColor", emissionColor * _currentEmission);
            yield return null;
        }
        _currentEmission = targetIntensity;
        _matInstance.SetColor("_EmissionColor", emissionColor * _currentEmission);
        _emissionCoroutine = null;
    }

    IEnumerator PanicFireRoutine(){
        float end = Time.time + panicDuration;
        while (Time.time < end){
            // pick a random muzzle to fire (or both randomly)
            if (muzzle2 != null){
                // randomly choose firing pattern: 0 = muzzle1, 1 = muzzle2, 2 = both
                int pattern = Random.Range(0, 3);
                if (pattern == 0) FirePanicFrom(muzzle, tracer);
                else if (pattern == 1) FirePanicFrom(muzzle2, tracer2);
                else { FirePanicFrom(muzzle, tracer); FirePanicFrom(muzzle2, tracer2); }
            } else {
                FirePanicFrom(muzzle, tracer);
            }

            // random small delay but biased by fireRate so it doesn't go absurdly fast
            float baseDelay = 1f / Mathf.Max(1f, fireRate);
            float delay = Random.Range(0.02f, baseDelay);
            yield return new WaitForSeconds(delay);
        }
        // ensure tracers are hidden after panic
        if (tracer) tracer.enabled = false;
        if (tracer2) tracer2.enabled = false;
        panicCoroutine = null;
        // after panic completes, disable the turret entirely
        if (debugLogs) Debug.Log("Panic complete: disabling turret", this);
        SetState(State.Sleep);
    }

    void FirePanicFrom(Transform muzz, LineRenderer tr){
        if (muzz == null) return;
        Vector3 dir = muzz.forward;

        float panicSpread = bulletSpreadDeg * extraSpreadMultiplier;
        dir = Quaternion.AngleAxis(Random.Range(-panicSpread, panicSpread), headPivot.up) * dir;
        dir = Quaternion.AngleAxis(Random.Range(-panicSpread, panicSpread), barrelPivot.right) * dir;

        // apply recoil during panic as well
        if (_rb != null && !_rb.isKinematic){
            // use the muzzle's forward as the canonical shot direction for recoil so the turret is pushed backward
            Vector3 recoilDir = -muzz.forward;
            if (useForceAtPosition) _rb.AddForceAtPosition(recoilDir.normalized * recoilForce, muzz.position, ForceMode.Impulse);
            else _rb.AddForce(recoilDir.normalized * recoilForce, ForceMode.Impulse);
            if (debugLogs) Debug.Log($"Applying panic recoil: dir={recoilDir.normalized}, force={recoilForce}", this);
        }

        if (Physics.Raycast(muzz.position, dir, out var hit, maxFireDist, sightMask, QueryTriggerInteraction.Ignore)){
            if (tr) ShowTracer(tr, muzz.position, hit.point);
            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null) dmg.ApplyDamage(bulletDamage, hit.point, hit.normal);
        } else {
            if (tr) ShowTracer(tr, muzz.position, muzz.position + dir * maxFireDist);
        }
    }

    void Die(){
        state = State.Dead;
        if (tracer != null) tracer.enabled = false;
        if (tracer2 != null) tracer2.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        enabled = false;
    }

}
