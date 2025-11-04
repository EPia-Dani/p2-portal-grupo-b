using System;
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

    private Collider _c;
    
    private Portal _inPortal;
    private Portal _outPortal;

    private void Start()
    {
        if (motor == null)
            motor = GetComponent<PlayerMotor>();
        if (look == null && Camera.main != null)
            look = GetComponent<PlayerLook>();
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
        _c = GetComponent<Collider>();
    }

    // usado por Portal para saber si estamos dentro y para cruzar el plano
    public void SetIsInPortal(Portal inPortal, Portal outPortal)
    {
        _inPortal = inPortal;
        _outPortal = outPortal;
        if (_inPortal.WallCollider != null)
            Physics.IgnoreCollision(_c, _inPortal.WallCollider, true);
        if (_outPortal.WallCollider != null)
            Physics.IgnoreCollision(_c, _outPortal.WallCollider, true);
        if(_inPortal.PortalCollider != null)
            _inPortal.PortalCollider.SetActive(true);
        if(_outPortal.PortalCollider != null)
            _outPortal.PortalCollider.SetActive(true);
    }

    public void ExitPortal()
    {
        if (_inPortal.WallCollider != null)
            Physics.IgnoreCollision(_c, _inPortal.WallCollider, false);
        if (_outPortal.WallCollider != null)
            Physics.IgnoreCollision(_c, _outPortal.WallCollider, false);
        if(_inPortal.PortalCollider != null)
            _inPortal.PortalCollider.SetActive(false);
        if(_outPortal.PortalCollider != null)
            _outPortal.PortalCollider.SetActive(false);
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

        // cámara (pitch): conservar inclinación relativa
        if (look != null)
        { 
            var inDir = -_inPortal.transform.forward;
            var outDir = _outPortal.transform.forward;
            
            Vector2 inXZ  = new Vector2(inDir.x,  inDir.z);
            Vector2 outXZ = new Vector2(outDir.x, outDir.z);

            float yawIn  = inXZ.sqrMagnitude > 1e-6f  ? Mathf.Atan2(inXZ.x,  inXZ.y)  * Mathf.Rad2Deg : 0f;
            float yawOut = outXZ.sqrMagnitude > 1e-6f ? Mathf.Atan2(outXZ.x, outXZ.y) * Mathf.Rad2Deg : 0f;
            float yawDelta = Mathf.DeltaAngle(yawIn, yawOut);

            // pitch from asin(y), clamped to [-1,1]
            float pitchIn  = Mathf.Asin(Mathf.Clamp(inDir.y,  -1f, 1f)) * Mathf.Rad2Deg;
            float pitchOut = Mathf.Asin(Mathf.Clamp(outDir.y, -1f, 1f)) * Mathf.Rad2Deg;
            float pitchDelta = pitchOut - pitchIn;
            
            if (float.IsFinite(yawDelta) && float.IsFinite(pitchDelta))
                look.AddYawPitchAbsolute(yawDelta, pitchDelta);
        }
        
        Vector3 inVel = motor.Velocity;
        
        Vector3 relVel = inT.InverseTransformDirection(inVel);
        relVel = HalfTurn * relVel;
        Vector3 outVel = outT.TransformDirection(relVel);
        
        float upAlign = Vector3.Dot(outT.forward.normalized, Vector3.up); // 0=horizontal, 1=straight up
        if (upAlign > 0.15f)
        {
            // scale boost with both the incline and entry speed
            float speedMag = inVel.magnitude;
            float boost = Mathf.Lerp(2f, 8f, upAlign) + 0.25f * speedMag;
            outVel += outT.forward * boost;
        }
        
        motor.SetVerticalVelocity(outVel.y);
        Vector3 horizontal = new Vector3(outVel.x, 0f, outVel.z);
        motor.InjectExternalVelocity(horizontal);
        
        ExitPortal();
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