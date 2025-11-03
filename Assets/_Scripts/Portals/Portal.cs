using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Portal : MonoBehaviour
{
    [field: SerializeField] public Portal OtherPortal { get; private set; }
    
    [SerializeField] private Renderer outlineRenderer;
    [field: SerializeField] public Color PortalColour { get; private set; }
    
    [SerializeField] private LayerMask placementMask;
    
    [SerializeField] private Transform testTransform;
    
    

    // CHANGED: keep legacy list for rigidbody-based objects
    private readonly List<PortalableObject> _portalObjects = new List<PortalableObject>();
    
    // CHANGED: add a list for the player (CharacterController-based)
    private readonly List<PortalablePlayer> _portalablePlayers = new List<PortalablePlayer>();
    
    private readonly List<PortalableCharacter> _portalChars = new List<PortalableCharacter>();


    public bool IsPlaced { get; private set; } = false;
    private Collider _wallCollider;

    public Renderer Renderer { get; private set; }
    private new BoxCollider _collider;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        Renderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        if (outlineRenderer != null)
        {
            outlineRenderer.material.SetColor("_OutlineColour", PortalColour);
        }
        gameObject.SetActive(false);
    }

    private void Update()
    {
        // CHANGED: null-guard OtherPortal
        Renderer.enabled = (OtherPortal != null && OtherPortal.IsPlaced);
        // SOLO PARA PROBAR
        Renderer.enabled = true;
        // Legacy rigidbody-based travellers
        for (int i = 0; i < _portalObjects.Count; ++i)
        {
            var t = _portalObjects[i].transform;
            Vector3 objPos = transform.InverseTransformPoint(t.position);
            if (objPos.z > 0.0f)
            {
                _portalObjects[i].Warp();
            }
        }

        // CHANGED: CharacterController-based player travellers
        for (int i = 0; i < _portalablePlayers.Count; ++i)
        {
            var t = _portalablePlayers[i].transform;
            Vector3 objPos = transform.InverseTransformPoint(t.position);
            if (objPos.z > 0.0f)
            {
                _portalablePlayers[i].Warp();
            }
        }
        
        for (int i = 0; i < _portalChars.Count; ++i)
        {
            if (_portalChars[i] != null && _portalChars[i].HasCrossedPlane(this))
                _portalChars[i].Warp();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // First, try rigidbody-based object
        var obj = other.GetComponent<PortalableObject>();
        if (obj != null)
        {
            if (!_portalObjects.Contains(obj)) _portalObjects.Add(obj);
            obj.SetIsInPortal(this, OtherPortal, _wallCollider);
            return;
        }

        // CHANGED: also support player with CharacterController via PortalablePlayer
        var player = other.GetComponentInParent<PortalablePlayer>();
        if (player != null)
        {
            if (!_portalablePlayers.Contains(player)) _portalablePlayers.Add(player);
            player.SetIsInPortal(this, OtherPortal, _wallCollider);
        }
        
        var chr = other.GetComponent<PortalableCharacter>();
        if (chr != null)
        {
            _portalChars.Add(chr);
            chr.SetIsInPortal(this, OtherPortal, _wallCollider);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var obj = other.GetComponent<PortalableObject>();
        if (obj != null)
        {
            if (_portalObjects.Contains(obj))
            {
                _portalObjects.Remove(obj);
                obj.ExitPortal(_wallCollider);
            }
            return;
        }

        // CHANGED: player exit handling
        var player = other.GetComponentInParent<PortalablePlayer>();
        if (player != null)
        {
            if (_portalablePlayers.Contains(player))
            {
                _portalablePlayers.Remove(player);
                player.ExitPortal(_wallCollider);
            }
        }
        
        
        var chr = other.GetComponent<PortalableCharacter>();
        if (chr != null && _portalChars.Contains(chr))
        {
            _portalChars.Remove(chr);
            chr.ExitPortal(_wallCollider);
        }
    }

    public bool PlacePortal(Collider wallCollider, Vector3 pos, Quaternion rot)
    {
        testTransform.position = pos;
        testTransform.rotation = rot;
        testTransform.position -= testTransform.forward * 0.001f;

        FixOverhangs();
        FixIntersects();

        if (CheckOverlap())
        {
            this._wallCollider = wallCollider;
            transform.position = testTransform.position;
            transform.rotation = testTransform.rotation;

            gameObject.SetActive(true);
            IsPlaced = true;
            return true;
        }

        return false;
    }

    // Ensure the portal cannot extend past the edge of a surface.
    private void FixOverhangs()
    {
        var testPoints = new List<Vector3>
        {
            new Vector3(-1.1f,  0.0f, 0.1f),
            new Vector3( 1.1f,  0.0f, 0.1f),
            new Vector3( 0.0f, -2.1f, 0.1f),
            new Vector3( 0.0f,  2.1f, 0.1f)
        };

        var testDirs = new List<Vector3>
        {
             Vector3.right,
            -Vector3.right,
             Vector3.up,
            -Vector3.up
        };

        for(int i = 0; i < 4; ++i)
        {
            RaycastHit hit;
            Vector3 raycastPos = testTransform.TransformPoint(testPoints[i]);
            Vector3 raycastDir = testTransform.TransformDirection(testDirs[i]);

            if(Physics.CheckSphere(raycastPos, 0.05f, placementMask))
            {
                break;
            }
            else if(Physics.Raycast(raycastPos, raycastDir, out hit, 2.1f, placementMask))
            {
                var offset = hit.point - raycastPos;
                testTransform.Translate(offset, Space.World);
            }
        }
    }

    // Ensure the portal cannot intersect a section of wall.
    private void FixIntersects()
    {
        var testDirs = new List<Vector3>
        {
             Vector3.right,
            -Vector3.right,
             Vector3.up,
            -Vector3.up
        };

        var testDists = new List<float> { 1.1f, 1.1f, 2.1f, 2.1f };

        for (int i = 0; i < 4; ++i)
        {
            RaycastHit hit;
            Vector3 raycastPos = testTransform.TransformPoint(0.0f, 0.0f, -0.1f);
            Vector3 raycastDir = testTransform.TransformDirection(testDirs[i]);

            if (Physics.Raycast(raycastPos, raycastDir, out hit, testDists[i], placementMask))
            {
                var offset = (hit.point - raycastPos);
                var newOffset = -raycastDir * (testDists[i] - offset.magnitude);
                testTransform.Translate(newOffset, Space.World);
            }
        }
    }

    // Once positioning has taken place, ensure the portal isn't intersecting anything.
    private bool CheckOverlap()
    {
        var checkExtents = new Vector3(0.9f, 1.9f, 0.05f);

        var checkPositions = new Vector3[]
        {
            testTransform.position + testTransform.TransformVector(new Vector3( 0.0f,  0.0f, -0.1f)),

            testTransform.position + testTransform.TransformVector(new Vector3(-1.0f, -2.0f, -0.1f)),
            testTransform.position + testTransform.TransformVector(new Vector3(-1.0f,  2.0f, -0.1f)),
            testTransform.position + testTransform.TransformVector(new Vector3( 1.0f, -2.0f, -0.1f)),
            testTransform.position + testTransform.TransformVector(new Vector3( 1.0f,  2.0f, -0.1f)),

            testTransform.TransformVector(new Vector3(0.0f, 0.0f, 0.2f))
        };

        // Ensure the portal does not intersect walls.
        var intersections = Physics.OverlapBox(checkPositions[0], checkExtents, testTransform.rotation, placementMask);

        if(intersections.Length > 1)
        {
            return false;
        }
        else if(intersections.Length == 1) 
        {
            // We are allowed to intersect the old portal position.
            if (intersections[0] != _collider)
            {
                return false;
            }
        }

        // Ensure the portal corners overlap a surface.
        bool isOverlapping = true;

        for(int i = 1; i < checkPositions.Length - 1; ++i)
        {
            isOverlapping &= Physics.Linecast(checkPositions[i], 
                checkPositions[i] + checkPositions[checkPositions.Length - 1], placementMask);
        }

        return isOverlapping;
    }

    public void RemovePortal()
    {
        gameObject.SetActive(false);
        IsPlaced = false;
    }
}