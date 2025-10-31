using UnityEngine;

public class CompanionCube : MonoBehaviour, IGrabbable
{
    [Header("Follow")]
    [SerializeField] float positionLerp = 0.25f;   // 0..1 of gap per tick
    [SerializeField] float rotationLerp = 0.25f;
    [SerializeField] float maxFollowDistance = 1f;

    [Header("Collision")]
    [SerializeField] float surfacePadding = 0.02f; // small skin to avoid interpenetration
    [SerializeField] LayerMask blockMask = ~0;     // adjust to ignore player layer if needed

    Rigidbody rb;
    Transform followTarget;
    bool isGrabbed;
    bool cachedUseGravity;
    CollisionDetectionMode cachedCD;
    RigidbodyInterpolation cachedInterp;

    GravityGun holdingGun;
    
    Vector3 storedVel;
    Vector3 storedAngVel;

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

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
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

        // continue with last follow momentum
        rb.linearVelocity = storedVel;
        rb.angularVelocity = storedAngVel;
    }

    void FixedUpdate()
    {
        if (!isGrabbed || followTarget == null) return;

        // hard release if the leash breaks
        float leash = Vector3.Distance(rb.position, followTarget.position);
        if (leash > maxFollowDistance) { OnRelease(); return; }

        // compute desired step this tick
        Vector3 goalPos = Vector3.Lerp(rb.position, followTarget.position, positionLerp);
        Vector3 step = goalPos - rb.position;

        if (step.sqrMagnitude > 1e-8f)
        {
            Vector3 dir = step.normalized;
            float dist = step.magnitude;
            
            if (Physics.Raycast(rb.position, dir, out RaycastHit hit, dist, blockMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 hitPos = hit.point - dir * surfacePadding;
                Vector3 move = hitPos - rb.position;

                storedVel = move / Time.fixedDeltaTime;
                rb.MovePosition(hitPos);
            }
            else
            {
                storedVel = step / Time.fixedDeltaTime;
                rb.MovePosition(goalPos);
            }

        }

        // rotate toward target; approximate angular velocity for release
        Quaternion nextRot = Quaternion.Slerp(rb.rotation, followTarget.rotation, rotationLerp);
        Quaternion delta = nextRot * Quaternion.Inverse(rb.rotation);
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (!float.IsNaN(axis.x))
        {
            float angleRad = angleDeg * Mathf.Deg2Rad;
            storedAngVel = axis.normalized * (angleRad / Time.fixedDeltaTime);
        }

        rb.MoveRotation(nextRot);
    }
}
