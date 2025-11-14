// csharp

using System;
using _Scripts.Interfaces;
using UnityEngine;

public class GrabbableBase : MonoBehaviour, IGrabbable
{
    [Header("Follow (spring)")]
    [SerializeField] float springStrength = 40f;    // higher = snappier pull
    [SerializeField] float springDamping = 8f;      // higher = less oscillation
    [SerializeField] float maxFollowSpeed = 40f;    // clamp speed

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
    [SerializeField] float minSpeed = 1f, damagePerKg = 2f;

    Rigidbody rb;
    bool isGrabbed;
    bool cachedUseGravity;
    CollisionDetectionMode cachedCD;
    RigidbodyInterpolation cachedInterp;

    GravityGun holdingGun;
    
    Vector3 targetPos;
    Quaternion targetRot = Quaternion.identity;
    bool hasTargetPose;

    Vector3 storedVel;
    Vector3 storedAngVel;

    // collision tracking: when > 0 the object is colliding
    int collisionCount = 0;
    
    public bool IsGrabbed => isGrabbed;
    public GravityGun HoldingGun => holdingGun;


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) Debug.LogError("CompanionCube needs a Rigidbody.");
    }

    public void OnGrab(GravityGun gravityGun)
    {
        holdingGun = gravityGun;
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
        Vector3 throwDir = hasTargetPose ? (targetRot * Vector3.forward).normalized
            : gravityGun.transform.forward;
        float throwSpeed = 5f;
        OnRelease();
        rb.linearVelocity = storedVel + throwDir * throwSpeed;
        rb.angularVelocity = storedAngVel;
    }

    public void SetTargetPose(Vector3 pos, Quaternion rot)
    {
        targetPos = pos;
        targetRot = rot;
        hasTargetPose = true;
    }

    void FixedUpdate()
    {
        if (!isGrabbed) return;
        float dt = Time.fixedDeltaTime;
        if (!hasTargetPose) return; // until first pose arrives

        // leash check
        float leash = Vector3.Distance(rb.position, targetPos);
        float currentMaxFollow = (collisionCount > 0) ? maxFollowDistance : Mathf.Infinity;
        if (leash > currentMaxFollow)
        {
            if (collisionCount > 0)
            {
                if (rb.linearVelocity.magnitude <= releaseSpeedThreshold) { OnRelease(); return; }
            }
            else { OnRelease(); return; }
        }

        // translation spring toward targetPos
        Vector3 toTarget = targetPos - rb.position;
        Vector3 desiredVel = toTarget * springStrength;
        Vector3 dampingVel = -rb.linearVelocity * springDamping;
        Vector3 nextVel = rb.linearVelocity + (desiredVel + dampingVel) * dt;
        if (nextVel.sqrMagnitude > maxFollowSpeed * maxFollowSpeed)
            nextVel = nextVel.normalized * maxFollowSpeed;

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
                nextVel = Vector3.zero;
            }
            else
            {
                storedVel = move / dt;
                rb.MovePosition(rb.position + move);
            }
        }
        else storedVel = Vector3.zero;

        rb.linearVelocity = nextVel;

        // rotation spring toward targetRot
        Quaternion deltaQ = targetRot * Quaternion.Inverse(rb.rotation);
        deltaQ.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-8f)
        {
            storedAngVel = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            float angleRad = Mathf.Deg2Rad * Mathf.Repeat(angleDeg + 180f, 360f) - Mathf.PI;
            Vector3 axisNorm = axis.normalized;
            Vector3 desiredAngVel = axisNorm * (angleRad * rotationStrength);
            Vector3 angDamping = -rb.angularVelocity * rotationDamping;
            Vector3 nextAngVel = rb.angularVelocity + (desiredAngVel + angDamping) * dt;
            if (nextAngVel.sqrMagnitude > maxAngularSpeed * maxAngularSpeed)
                nextAngVel = nextAngVel.normalized * maxAngularSpeed;

            Quaternion step = Quaternion.Euler(Mathf.Rad2Deg * dt * nextAngVel);
            Quaternion nextRot = step * rb.rotation;

            storedAngVel = nextAngVel;
            rb.MoveRotation(nextRot);
            rb.angularVelocity = nextAngVel;
        }
    }
    
    void OnCollisionEnter(Collision c){
        Debug.Log("Collision Enter");
        if (rb.linearVelocity.magnitude < minSpeed) return;
        var dmg = damagePerKg * rb.mass * rb.linearVelocity.magnitude;
        dmg = Mathf.FloorToInt(dmg);
        c.collider.GetComponentInParent<IDamageable>()?.ApplyDamage(dmg, c.GetContact(0).point, c.GetContact(0).normal);
    }
    
}
