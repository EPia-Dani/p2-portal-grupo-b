using _Scripts.Interfaces;
using UnityEngine;
using _Scripts.Player.Runtime;
using _Scripts.Portals;
    
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PortalableCharacter : PortalableBase
{
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private PlayerLook look;
    [SerializeField] private Transform head;

    Collider _c;
    CharacterKinematics _kin;
    
    protected override bool BaseAppliesRotation => false; 

    protected void Awake()
    {
        if (!motor) motor = GetComponent<PlayerMotor>();
        if (!look)
        {
            look = GetComponentInChildren<PlayerLook>();
            if (!look && Camera.main) look = Camera.main.GetComponent<PlayerLook>();
        }
        if (!head && Camera.main) head = Camera.main.transform;

        _c = GetComponent<Collider>();
        _kin = new CharacterKinematics(transform, motor, ApplyAbsoluteYawPitchFromQuaternion);
    }


    protected override IPortalKinematics Kin => _kin;
    protected override Vector3 PlaneProbePosition => head ? head.position : transform.position;
    protected override Collider GetMainCollider() => _c;
    
    protected override void OnAfterWarp(Transform inT, Transform outT, Quaternion deltaRot)
    {

        // compute target camera orientation after warp
        Quaternion targetHeadRot = deltaRot * (head ? head.rotation : look.transform.rotation);
        
        void ToYawPitch(Quaternion rot, out float yawDeg, out float pitchDeg)
        {
            Vector3 f = rot * Vector3.forward;
            Vector3 xz = new Vector3(f.x, 0f, f.z);
            yawDeg   = xz.sqrMagnitude > 1e-6f ? Mathf.Atan2(xz.x, xz.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
            pitchDeg = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        ToYawPitch(targetHeadRot, out float yawAbs, out float pitchAbs);
        look.SetYawPitchAbsolute(yawAbs, -pitchAbs);
        
    }

    void ApplyAbsoluteYawPitchFromQuaternion(Quaternion q)
    {
        // Delegate to PlayerLook if you prefer, otherwise set transform.rotation.
        transform.rotation = q;
    }
}