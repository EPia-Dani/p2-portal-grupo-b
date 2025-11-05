using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PortalableObject : MonoBehaviour
{
    private GameObject _cloneObject;

    private int _inPortalCount = 0;
    
    private Portal _inPortal;
    private Portal _outPortal;

    private Rigidbody _rigidbody;
    protected new Collider collider;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0.0f, 180.0f, 0.0f);

    protected virtual void Awake()
    {
        _cloneObject = new GameObject();
        _cloneObject.SetActive(false);
        var meshFilter = _cloneObject.AddComponent<MeshFilter>();
        var meshRenderer = _cloneObject.AddComponent<MeshRenderer>();

        meshFilter.mesh = GetComponent<MeshFilter>().mesh;
        meshRenderer.materials = GetComponent<MeshRenderer>().materials;
        _cloneObject.transform.localScale = transform.localScale;

        _rigidbody = GetComponent<Rigidbody>();
        collider = GetComponent<Collider>();
    }

    private void LateUpdate()
    {
        if(_inPortal == null || _outPortal == null)
        {
            return;
        }

        if(_cloneObject.activeSelf && _inPortal.IsPlaced && _outPortal.IsPlaced)
        {
            var inTransform = _inPortal.transform;
            var outTransform = _outPortal.transform;

            // Update position of clone.
            Vector3 relativePos = inTransform.InverseTransformPoint(transform.position);
            relativePos = HalfTurn * relativePos;
            _cloneObject.transform.position = outTransform.TransformPoint(relativePos);

            // Update rotation of clone.
            Quaternion relativeRot = Quaternion.Inverse(inTransform.rotation) * transform.rotation;
            relativeRot = HalfTurn * relativeRot;
            _cloneObject.transform.rotation = outTransform.rotation * relativeRot;
        }
        else
        {
            _cloneObject.transform.position = new Vector3(-1000.0f, 1000.0f, -1000.0f);
        }
    }

    public void SetIsInPortal(Portal inPortal, Portal outPortal, Collider wallCollider)
    {
        this._inPortal = inPortal;
        this._outPortal = outPortal;

        Physics.IgnoreCollision(collider, wallCollider);

        _cloneObject.SetActive(false);

        ++_inPortalCount;
    }

    public void ExitPortal(Collider wallCollider)
    {
        Physics.IgnoreCollision(collider, wallCollider, false);
        --_inPortalCount;

        if (_inPortalCount == 0)
        {
            _cloneObject.SetActive(false);
        }
    }

    public virtual void Warp()
    {
        var inTransform = _inPortal.transform;
        var outTransform = _outPortal.transform;

        // Update position of object.
        Vector3 relativePos = inTransform.InverseTransformPoint(transform.position);
        relativePos = HalfTurn * relativePos;
        transform.position = outTransform.TransformPoint(relativePos);

        // Update rotation of object.
        Quaternion relativeRot = Quaternion.Inverse(inTransform.rotation) * transform.rotation;
        relativeRot = HalfTurn * relativeRot;
        transform.rotation = outTransform.rotation * relativeRot;

        Vector3 relativeVel = inTransform.InverseTransformDirection(_rigidbody.linearVelocity);
        relativeVel = HalfTurn * relativeVel;
        _rigidbody.linearVelocity = outTransform.TransformDirection(relativeVel);

        // Swap portal references.
        var tmp = _inPortal;
        _inPortal = _outPortal;
        _outPortal = tmp;
    }
}