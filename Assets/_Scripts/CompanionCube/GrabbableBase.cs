// csharp
using UnityEngine;

public class GrabbableBase : MonoBehaviour, IGrabbable
{
    [Header("Follow (spring)")]
    [SerializeField] float springStrength = 40f;    // higher = snappier pull
    [SerializeField] float springDamping = 8f;      // higher = less oscillation
    [SerializeField] float maxFollowSpeed = 20f;    // clamp speed

    [Header("Rotation (angular spring)")]
    [SerializeField] float rotationStrength = 40f;  // angular stiffness
    [SerializeField] float rotationDamping = 6f;    // angular damping
    [SerializeField] float maxAngularSpeed = 40f;   // clamp angular speed (rad/s)

    [Header("Follow")]
    [SerializeField] float maxFollowDistance = 1f;
    [Tooltip("When colliding and beyond the leash, only release if linear speed is below this threshold.")]
    [SerializeField] float releaseSpeedThreshold = 0.5f; // meters per second

    [Header("Collision")]
    [SerializeField] float surfacePadding = 0.02f;
    [SerializeField] LayerMask blockMask = ~0;

    Rigidbody rb;
    Transform followTarget;
    bool isGrabbed;
    bool cachedUseGravity;
    CollisionDetectionMode cachedCD;
    RigidbodyInterpolation cachedInterp;

    GravityGun holdingGun;

    Vector3 storedVel;
    Vector3 storedAngVel;

    // collision tracking: when > 0 the object is colliding
    int collisionCount = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) Debug.LogError("CompanionCube needs a Rigidbody.");
    }

    public void OnGrab(GravityGun gravityGun)
    {
        holdingGun = gravityGun;
        followTarget = gravityGun.HoldPoint;
        isGrabbed = true;

        cachedUseGravity = rb.useGravity;
        cachedCD = rb.collisionDetectionMode;
        cachedInterp = rb.interpolation;

        rb.useGravity = false;
        rb.linearDamping = 8f;
        rb.angularDamping = 8f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // stop immediate motion
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        storedVel = Vector3.zero;
        storedAngVel = Vector3.zero;
    }

    public void OnRelease()
    {
        holdingGun?.Release();
        holdingGun = null;
        isGrabbed = false;
        followTarget = null;

        rb.useGravity = cachedUseGravity;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.05f;
        rb.collisionDetectionMode = cachedCD;
        rb.interpolation = cachedInterp;

        // apply the last tracked velocities so it carries momentum
        rb.linearVelocity = storedVel;
        rb.angularVelocity = storedAngVel;
    }

    public void OnThrow(GravityGun gravityGun)
    {
        if (gravityGun == null)
            return;
        
        Vector3 throwDir = gravityGun.HoldPoint.forward.normalized;
        float throwSpeed = 15f;
        
        OnRelease();
        
        rb.linearVelocity = storedVel + throwDir * throwSpeed;
        rb.angularVelocity = storedAngVel;
    }

    void FixedUpdate()
    {
        if (!isGrabbed || followTarget == null) return;

        float dt = Time.fixedDeltaTime;

        // leash check: if colliding, max follow distance = maxFollowDistance, otherwise infinite
        float leash = Vector3.Distance(rb.position, followTarget.position);
        float currentMaxFollow = (collisionCount > 0) ? maxFollowDistance : Mathf.Infinity;
        if (leash > currentMaxFollow)
        {
            if (collisionCount > 0)
            {
                // Only release if the object is not sliding/moving (speed below threshold)
                if (rb.linearVelocity.magnitude <= releaseSpeedThreshold)
                {
                    OnRelease();
                    return;
                }
                // else: still colliding and moving -> keep holding despite leash exceeded
            }
            else
            {
                // not colliding and beyond leash (shouldn't happen because currentMaxFollow is Infinity),
                // but keep the original behavior safe.
                OnRelease();
                return;
            }
        }

        // spring force for translation: compute desired acceleration-like velocity change
        Vector3 toTarget = followTarget.position - rb.position;
        float dist = toTarget.magnitude;

        // desired velocity from spring (proportional to displacement)
        Vector3 desiredVel = toTarget * springStrength;
        // damping proportional to current velocity
        Vector3 dampingVel = -rb.linearVelocity * springDamping;
        // integrate velocity
        Vector3 nextVel = rb.linearVelocity + (desiredVel + dampingVel) * dt;

        // clamp speed
        if (nextVel.sqrMagnitude > maxFollowSpeed * maxFollowSpeed)
            nextVel = nextVel.normalized * maxFollowSpeed;

        // compute intended move for this tick
        Vector3 move = nextVel * dt;
        if (move.sqrMagnitude > 1e-8f)
        {
            Vector3 dir = move.normalized;
            float moveDist = move.magnitude;

            if (Physics.Raycast(rb.position, dir, out RaycastHit hit, moveDist, blockMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 hitPos = hit.point - dir * surfacePadding;
                storedVel = (hitPos - rb.position) / dt;
                rb.MovePosition(hitPos);
                // zero out nextVel to avoid pushing into obstacle next frame
                nextVel = Vector3.zero;
            }
            else
            {
                storedVel = move / dt;
                rb.MovePosition(rb.position + move);
            }
        }
        else
        {
            // tiny motion; still set storedVel
            storedVel = Vector3.zero;
        }

        // write back velocity tracking to the rigidbody if needed (keep physics consistent)
        rb.linearVelocity = nextVel;

        // angular spring: compute target angular velocity needed to rotate toward followTarget
        Quaternion deltaQ = followTarget.rotation * Quaternion.Inverse(rb.rotation);
        deltaQ.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-8f)
        {
            // no significant rotation needed
            storedAngVel = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            float angleRad = Mathf.Deg2Rad * Mathf.Repeat(angleDeg + 180f, 360f) - Mathf.PI; // signed shortest angle
            Vector3 axisNorm = axis.normalized;
            // desired angular velocity (proportional to angle)
            Vector3 desiredAngVel = axisNorm * (angleRad * rotationStrength);
            // damping
            Vector3 angDamping = -rb.angularVelocity * rotationDamping;
            Vector3 nextAngVel = rb.angularVelocity + (desiredAngVel + angDamping) * dt;

            // clamp
            if (nextAngVel.sqrMagnitude > maxAngularSpeed * maxAngularSpeed)
                nextAngVel = nextAngVel.normalized * maxAngularSpeed;

            // integrate rotation directly to keep in sync
            Quaternion step = Quaternion.Euler(Mathf.Rad2Deg * dt * nextAngVel);
            Quaternion nextRot = step * rb.rotation;

            // collision-safe rotation is less straightforward; just apply rotation
            storedAngVel = nextAngVel;
            rb.MoveRotation(nextRot);
            rb.angularVelocity = nextAngVel;
        }
    }

    void OnCollisionEnter(Collision other)
    {
        collisionCount++;
    }

    void OnCollisionExit(Collision other)
    {
        collisionCount = Mathf.Max(0, collisionCount - 1);
    }
}
