using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Portal : MonoBehaviour
{
    [field: SerializeField] public Portal OtherPortal { get; private set; }
    
    [SerializeField] private Renderer outlineRenderer;
    [field: SerializeField] public Color PortalColour { get; private set; }
    
    [SerializeField] private Renderer screenRenderer; 
    
    [SerializeField] private LayerMask placementMask;
    
    [SerializeField] private Transform testTransform;
    
    
    

    // CHANGED: keep legacy list for rigidbody-based objects
    private readonly List<PortalableObject> _portalObjects = new List<PortalableObject>();
    
    // CHANGED: add a list for the player (CharacterController-based)
    private readonly List<PortalablePlayer> _portalablePlayers = new List<PortalablePlayer>();
    
    private readonly List<PortalableCharacter> _portalChars = new List<PortalableCharacter>();


    public bool IsPlaced { get; private set; } = false;
    private Collider _wallCollider;
    public Collider WallCollider => _wallCollider;
    
    [SerializeField] private GameObject portalCollider;
    public GameObject PortalCollider => portalCollider;
    
    private Renderer _fallbackRenderer;              
    public Renderer Renderer => screenRenderer != null ? screenRenderer : _fallbackRenderer;
    private BoxCollider _collider;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        _collider.isTrigger = true;                 
        _fallbackRenderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        if (outlineRenderer != null)
        {
            outlineRenderer.material.SetColor("_OutlineColour", PortalColour);
        }
        if (Renderer != null)
            Renderer.enabled = false;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        // Mostrar/ocultar la pantalla del portal en función del estado del otro portal
        bool visible = (OtherPortal != null && OtherPortal.IsPlaced);
        if (Renderer != null) Renderer.enabled = visible;

        // DEBUG: estado de visibilidad (no spamea si no cambia)
        Debug.Log($"[Portal:{name}] Visible={visible} (OtherPortal={(OtherPortal ? OtherPortal.name : "null")})");

        // Objetos con Rigidbody
        for (int i = 0; i < _portalObjects.Count; ++i)
        {
            var t = _portalObjects[i].transform;
            Vector3 objPos = transform.InverseTransformPoint(t.position);
            if (objPos.z > 0.0f)
            {
                Debug.Log($"[Portal:{name}] Warp() PortalableObject -> {_portalObjects[i].name}");
                _portalObjects[i].Warp();
            }
        }

        // CharacterController con cabeza/pivot (PortalableCharacter)
        for (int i = 0; i < _portalChars.Count; ++i)
        {
            var chr = _portalChars[i];
            if (chr != null && chr.HasCrossedPlane(this) && OtherPortal != null)
            {
                Debug.Log($"[Portal:{name}] HasCrossedPlane -> Warp() {chr.name}");
                chr.Warp();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[Portal:{name}] OnTriggerEnter con {other.name} (layer={LayerMask.LayerToName(other.gameObject.layer)})");

        // 1) Objetos con Rigidbody
        var obj = other.GetComponent<PortalableObject>();
        if (obj != null)
        {
            if (!_portalObjects.Contains(obj)) _portalObjects.Add(obj);
            obj.SetIsInPortal(this, OtherPortal);
            Debug.Log($"[Portal:{name}] Registrado PortalableObject: {obj.name} (count={_portalObjects.Count})");
            return;
        }

        // 2) Player basado en CharacterController (root)
        var player = other.GetComponentInParent<PortalablePlayer>();
        if (player != null)
        {
            if (!_portalablePlayers.Contains(player)) _portalablePlayers.Add(player);
            player.SetIsInPortal(this, OtherPortal, _wallCollider);
            Debug.Log($"[Portal:{name}] Registrado PortalablePlayer: {player.name} (count={_portalablePlayers.Count})");
            return;
        }

        // 3) CharacterController con cabeza/pivot (normalmente en el mismo GO del player)
        var chr = other.GetComponent<PortalableCharacter>();
        if (chr != null)
        {
            if (!_portalChars.Contains(chr)) _portalChars.Add(chr);
            chr.SetIsInPortal(this, OtherPortal);
            Debug.Log($"[Portal:{name}] Registrado PortalableCharacter: {chr.name} (count={_portalChars.Count})");
            return;
        }

        // Si llega aquí, es un collider que no nos interesa
         Debug.Log($"[Portal:{name}] TriggerEnter ignorado para {other.name}");
    }
    private void OnTriggerExit(Collider other)
    {
        Debug.Log($"[Portal:{name}] OnTriggerExit con {other.name}");

        var obj = other.GetComponent<PortalableObject>();
        if (obj != null)
        {
            if (_portalObjects.Contains(obj))
            {
                _portalObjects.Remove(obj);
                obj.ExitPortal();
                Debug.Log($"[Portal:{name}] Unregistered PortalableObject: {obj.name} (count={_portalObjects.Count})");
            }
            return;
        }

        var player = other.GetComponentInParent<PortalablePlayer>();
        if (player != null)
        {
            if (_portalablePlayers.Contains(player))
            {
                _portalablePlayers.Remove(player);
                player.ExitPortal(_wallCollider);
                Debug.Log($"[Portal:{name}] Unregistered PortalablePlayer: {player.name} (count={_portalablePlayers.Count})");
            }
            return;
        }

        var chr = other.GetComponent<PortalableCharacter>();
        if (chr != null && _portalChars.Contains(chr))
        {
            _portalChars.Remove(chr);
            chr.ExitPortal();
            Debug.Log($"[Portal:{name}] Unregistered PortalableCharacter: {chr.name} (count={_portalChars.Count})");
        }

        // Debug opcional si quieres ver salidas “no relevantes”
        // Debug.Log($"[Portal:{name}] TriggerExit ignorado para {other.name}");
    }
    public bool PlacePortal(Collider wallCollider, Vector3 pos, Quaternion rot)
    {
        // Pre-posicionado de prueba
        testTransform.position = pos;
        testTransform.rotation = rot;
        testTransform.position -= testTransform.forward * 0.001f;

        // Ajustes de borde/intersecciones
        FixOverhangs();
        FixIntersects();

        bool ok = CheckOverlap();
        Debug.Log($"[Portal:{name}] PlacePortal en {pos} rot={rot.eulerAngles} -> {(ok ? "OK" : "FALLO (overlap/intersección/borde)")}");

        if (ok)
        {
            this._wallCollider = wallCollider;
            transform.position = testTransform.position;
            transform.rotation = testTransform.rotation;

            gameObject.SetActive(true);
            IsPlaced = true;

            // Forzar que la pantalla solo se vea si el otro está colocado
            if (Renderer != null)
                Renderer.enabled = (OtherPortal != null && OtherPortal.IsPlaced);

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