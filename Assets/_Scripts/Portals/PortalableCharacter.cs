using System.Collections.Generic;
using UnityEngine;
using _Scripts.Player.Runtime; // PlayerMotor, PlayerLook

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PortalableCharacter : MonoBehaviour
{
    // refs
    [SerializeField] private PlayerMotor motor;   // arrástralo (está en el root del player)
    [SerializeField] private PlayerLook look;     // arrastra la MainCamera que tiene PlayerLook
    [SerializeField] private Transform head;      // la cámara o el pivot de la cabeza (cruce plano)

    private Portal _inPortal;
    private Portal _outPortal;

    // usado por Portal para saber si estamos dentro y para cruzar el plano
    public void SetIsInPortal(Portal inPortal, Portal outPortal, Collider wallCollider)
    {
        _inPortal = inPortal;
        _outPortal = outPortal;
        // ignorar colisión con la pared mientras estemos dentro del trigger
        if (wallCollider != null && TryGetComponent<Collider>(out var c))
            Physics.IgnoreCollision(c, wallCollider, true);
    }

    public void ExitPortal(Collider wallCollider)
    {


        _inPortal = null;
        _outPortal = null;
    }

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    public void Warp()
    {
        Debug.Log("[PortalableCharacter] WARP ejecutado");
        if (_inPortal == null || _outPortal == null || motor == null) return;

        // --- Posición (usa PlayerMotor.TeleportTo para gestionar CharacterController) ---
        var inT  = _inPortal.transform;
        var outT = _outPortal.transform;

        Vector3 relativePos = inT.InverseTransformPoint(transform.position);
        relativePos = HalfTurn * relativePos;
        Vector3 newWorldPos = outT.TransformPoint(relativePos);
        
        
        newWorldPos += outT.forward * 0.01f;   


        motor.TeleportTo(newWorldPos); // ya desactiva/activa el CharacterController

        // --- Rotación (yaw/pitch) ---
        // cuerpo (yaw): rota como el portal
        Quaternion relativeRot = Quaternion.Inverse(inT.rotation) * transform.rotation;
        relativeRot = HalfTurn * relativeRot;
        Quaternion newWorldRot = outT.rotation * relativeRot;

        transform.rotation = Quaternion.Euler(0f, newWorldRot.eulerAngles.y, 0f);

        // cámara (pitch): conservar inclinación relativa
        if (look != null)
        {
            // extraemos pitch relativo de la cámara respecto al cuerpo antes del warp
            float pitchBefore = head != null
                ? head.localEulerAngles.x
                : 0f;

            // normaliza pitch a [-180,180] y clampará internamente
            if (pitchBefore > 180f) pitchBefore -= 360f;

            // “snap” del look para que sus internos (yaw/pitch) coincidan inmediatamente
            look.SetYawPitchAbsolute(transform.eulerAngles.y, pitchBefore);
        }

        
        // swap de portales (igual que en PortalableObject)
        var tmp = _inPortal;
        _inPortal = _outPortal;
        _outPortal = tmp;
    }

    // Utilidad para que el Portal compruebe si hemos cruzado el plano
    public bool HasCrossedPlane(Portal portal)
    {
        if (head == null) head = Camera.main ? Camera.main.transform : transform;
        Vector3 local = portal.transform.InverseTransformPoint(head.position);
        Debug.Log($"[PortalableCharacter] local.z={local.z}");
        return local.z > 0f;
    }
}