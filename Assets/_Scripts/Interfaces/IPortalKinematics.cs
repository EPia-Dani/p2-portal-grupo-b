using UnityEngine;

namespace _Scripts.Interfaces
{
    public interface IPortalKinematics
    {
        Vector3 Position { get; set; }
        Quaternion Rotation { get; set; }

        // World-space velocity. For CharacterController use your motor’s composite velocity.
        Vector3 Velocity { get; set; }

        // Use when a CharacterController requires special teleport handling.
        void Teleport(Vector3 worldPos);
    }
}