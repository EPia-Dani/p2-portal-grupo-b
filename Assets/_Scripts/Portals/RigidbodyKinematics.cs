namespace _Scripts.Portals
{    
    using UnityEngine;
    using Interfaces;
    
    public sealed class RigidbodyKinematics : IPortalKinematics
    {
        readonly Rigidbody rb;
        public RigidbodyKinematics(Rigidbody rb) { this.rb = rb; }

        public Vector3 Position { get => rb.position; set => rb.position = value; }
        public Quaternion Rotation { get => rb.rotation; set => rb.rotation = value; }
        public Vector3 Velocity { get => rb.linearVelocity; set => rb.linearVelocity = value; }
        public void Teleport(Vector3 worldPos)
        {
            rb.interpolation = RigidbodyInterpolation.None;
            rb.MovePosition(worldPos);
        }
    }

}