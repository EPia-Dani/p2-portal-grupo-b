// --- PortalablePlayer.cs (nuevo) ---
using UnityEngine;

[RequireComponent(typeof(_Scripts.Player.Runtime.PlayerMotor))]
public class PortalablePlayer : MonoBehaviour
{
    private Portal _inPortal;
    private Portal _outPortal;
    private int _inPortalCount = 0;

    private _Scripts.Player.Runtime.PlayerMotor _motor;
    private _Scripts.Player.Runtime.PlayerLook _look;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    private void Awake()
    {
        _motor = GetComponent<_Scripts.Player.Runtime.PlayerMotor>();
        _look  = GetComponentInChildren<_Scripts.Player.Runtime.PlayerLook>(true);
    }

    public void SetIsInPortal(Portal inPortal, Portal outPortal, Collider wallCollider)
    {
        this._inPortal = inPortal;
        this._outPortal = outPortal;
        // CharacterController no usa IgnoreCollision; lo gestiona el propio Portal si es necesario
        ++_inPortalCount;
    }

    public void ExitPortal(Collider wallCollider)
    {
        --_inPortalCount;
    }

    public void Warp()
    {
        if (_inPortal == null || _outPortal == null) return;

        var inT  = _inPortal.transform;
        var outT = _outPortal.transform;

        // Position
        Vector3 relativePos = inT.InverseTransformPoint(transform.position);
        relativePos = HalfTurn * relativePos;
        Vector3 newPos = outT.TransformPoint(relativePos);

        // Rotation
        Quaternion relativeRot = Quaternion.Inverse(inT.rotation) * transform.rotation;
        relativeRot = HalfTurn * relativeRot;
        Quaternion newRot = outT.rotation * relativeRot;

        // Teleport using PlayerMotor (refresh CC state)
        _motor.TeleportTo(newPos);
        transform.rotation = newRot;

        if (_look != null)
        {
            _look.ResetToCurrentForward();
        }

        // Swap portals for potential re-entry
        var tmp = _inPortal;
        _inPortal = _outPortal;
        _outPortal = tmp;
    }
}