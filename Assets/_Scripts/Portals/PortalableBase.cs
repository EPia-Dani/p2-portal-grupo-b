namespace _Scripts.Portals
{
using UnityEngine;
using Interfaces;
public abstract class PortalableBase : MonoBehaviour, IPortalable
{
    protected Portal inPortal;
    protected Portal outPortal;
    protected static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    // Inject concrete kinematics in subclasses
    protected abstract IPortalKinematics Kin { get; }

    // Optional: subclasses can override which point tests the plane (head vs body)
    protected virtual Vector3 PlaneProbePosition => transform.position;
    
    protected virtual bool BaseAppliesRotation => true;

    // Optional post-warp adjustments (camera yaw/pitch, clone toggles, etc.)
    protected virtual void OnAfterWarp(Transform inT, Transform outT, Quaternion deltaRot) { }
    
    protected virtual void OnEnterPortal() { }
    protected virtual void OnExitPortal() { }

    public virtual void SetIsInPortal(Portal inPortal, Portal outPortal)
    {
        this.inPortal = inPortal;
        this.outPortal = outPortal;
        
        if (this.inPortal?.WallCollider) Physics.IgnoreCollision(GetMainCollider(), this.inPortal.WallCollider, true);
        if (this.outPortal?.WallCollider) Physics.IgnoreCollision(GetMainCollider(), this.outPortal.WallCollider, true);
        if (this.inPortal?.PortalCollider)  this.inPortal.PortalCollider.SetActive(true);
        if (this.outPortal?.PortalCollider) this.outPortal.PortalCollider.SetActive(true);

        OnEnterPortal();
    }

    public virtual void ExitPortal()
    {
        if (inPortal?.WallCollider) Physics.IgnoreCollision(GetMainCollider(), inPortal.WallCollider, false);
        if (outPortal?.WallCollider) Physics.IgnoreCollision(GetMainCollider(), outPortal.WallCollider, false);
        if (inPortal?.PortalCollider)  inPortal.PortalCollider.SetActive(false);
        if (outPortal?.PortalCollider) outPortal.PortalCollider.SetActive(false);

        inPortal = null;
        outPortal = null;

        OnExitPortal();
    }

    public virtual void Warp()
    {
        if (inPortal == null || outPortal == null) return;

        Transform inT  = inPortal.transform;
        Transform outT = outPortal.transform;

        // Position
        Vector3 relPos = inT.InverseTransformPoint(Kin.Position);
        relPos = HalfTurn * relPos;
        Vector3 newPos = outT.TransformPoint(relPos);
        newPos += outT.forward * 0.01f; // slight push to avoid re-trigger

        Kin.Teleport(newPos);

        // Rotation
        Quaternion deltaRot = outT.rotation * HalfTurn * Quaternion.Inverse(inT.rotation);
        if (Kin is RigidbodyKinematics)
        {
            var rb = ((RigidbodyKinematics)Kin).GetType()
                .GetField("rb", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(Kin) as Rigidbody;

            if (rb)
            {
                rb.MoveRotation(deltaRot * rb.rotation);
            }
            else
            {
                Kin.Rotation = deltaRot * Kin.Rotation;
            }
        }
        else if (BaseAppliesRotation)
        {
            Kin.Rotation = deltaRot * Kin.Rotation;
        }
        
        // Velocity + boost rule (shared)
        Vector3 inVel = Kin.Velocity;
        Vector3 relVel = inT.InverseTransformDirection(inVel);
        relVel = HalfTurn * relVel;
        Vector3 outVel = outT.TransformDirection(relVel);

        float upAlign = Vector3.Dot(outT.forward.normalized, Vector3.up);
        if (upAlign > 0f)
        {
            float speedMag = inVel.magnitude;
            float boost = Mathf.Lerp(2f, 8f, upAlign) + 0.25f * speedMag;
            outVel -= outT.forward * boost;
        }
        Kin.Velocity = outVel;

        // Swap portals so re-entry works
        ( inPortal, outPortal ) = ( outPortal, inPortal );

        OnAfterWarp(inT, outT, deltaRot);
    }

    public virtual bool HasCrossedPlane(Portal portal)
    {
        if (portal == null) return false;
        Vector3 local = portal.transform.InverseTransformPoint(PlaneProbePosition);
        return local.z > 0f;
    }

    // Subclasses provide the collider used for Physics.IgnoreCollision
    protected abstract Collider GetMainCollider();
}

}