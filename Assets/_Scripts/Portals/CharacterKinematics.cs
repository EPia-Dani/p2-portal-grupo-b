namespace _Scripts.Portals
{
    using Player.Runtime;
    using UnityEngine;
    using Interfaces;

    public sealed class CharacterKinematics : IPortalKinematics
    {
        readonly Transform root;
        readonly PlayerMotor motor;
        readonly System.Action<Quaternion> setWorldRot;

        public CharacterKinematics(Transform root, PlayerMotor motor, System.Action<Quaternion> setWorldRot = null)
        {
            this.root = root;
            this.motor = motor;
            this.setWorldRot = setWorldRot;
        }

        public Vector3 Position { get => root.position; set => root.position = value; }
        public Quaternion Rotation
        {
            get => root.rotation;
            set
            {
                if(setWorldRot != null)
                    setWorldRot.Invoke(value);
                else
                {
                    root.rotation = value;
                }
            }
        }

        public Vector3 Velocity
        {
            get => motor.Velocity;
            set
            {
                // split like your current flow: vertical via SetVerticalVelocity + horizontal inject
                motor.SetVerticalVelocity(value.y);
                Vector3 h = new Vector3(value.x, 0f, value.z);
                motor.InjectExternalVelocity(h);
            }
        }

        public void Teleport(Vector3 worldPos) => motor.TeleportTo(worldPos);
    }

}