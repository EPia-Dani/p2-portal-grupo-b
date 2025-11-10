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
        if (!head && Camera.main) head = Camera.main.transform;
        _c = GetComponent<Collider>();
        _kin = new CharacterKinematics(transform, motor, ApplyAbsoluteYawPitchFromQuaternion);
    }

    protected override IPortalKinematics Kin => _kin;
    protected override Vector3 PlaneProbePosition => head ? head.position : transform.position;
    protected override Collider GetMainCollider() => _c;

    protected override void OnAfterWarp(Transform inT, Transform outT, Quaternion deltaRot)
    {
        if (!look || !head) { ExitPortal(); return; }

        // Guardar rotación local de la cabeza respecto al cuerpo antes del warp
        Quaternion localHead = Quaternion.Inverse(transform.rotation) * head.rotation;

        // Aplicar la rotación delta al cuerpo (mantiene la relación local cabeza->cuerpo)
        transform.rotation = deltaRot * transform.rotation;

        // Reconstruir la rotación mundial de la cabeza tras rotar el cuerpo
        Quaternion targetHeadWorld = transform.rotation * localHead;

        Vector3 f = targetHeadWorld * Vector3.forward;
        Vector3 xz = new Vector3(f.x, 0f, f.z);
        float yawDeg   = xz.sqrMagnitude > 1e-8f ? Mathf.Atan2(xz.x, xz.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
        float pitchDeg = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;

        look.SetYawPitchAbsolute(yawDeg, -pitchDeg);

        ExitPortal();
    }




    void ApplyAbsoluteYawPitchFromQuaternion(Quaternion q)
    {
        // Delegate to PlayerLook if you prefer, otherwise set transform.rotation.
        transform.rotation = q;
    }
}