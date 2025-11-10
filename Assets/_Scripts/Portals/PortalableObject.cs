using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PortalableObject : MonoBehaviour
{
    // Clone para la vista a través del portal
    private GameObject _cloneObject;

    private int _inPortalCount = 0;

    private Portal _inPortal;
    private Portal _outPortal;

    private Rigidbody _rigidbody;
    private Collider _collider;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    // Tope de caída consistente con PlayerMotor
    private const float MAX_FALL_SPEED = -20f;

    protected virtual void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();

        _cloneObject = new GameObject($"{name}_Clone");
        _cloneObject.SetActive(false);

        // Asegurarse de que el clon no interfiera con físicas
        if (!_cloneObject.TryGetComponent<BoxCollider>(out var cloneCollider))
            cloneCollider = _cloneObject.AddComponent<BoxCollider>();
        cloneCollider.enabled = false;

        // Recolectar y combinar todos los meshes (MeshFilter + SkinnedMeshRenderer)
        var combineList = new List<CombineInstance>();
        var materials = new List<Material>();
        var tempBakedMeshes = new List<Mesh>();

        // Nota: transformar cada mesh al espacio local de este objeto
        Matrix4x4 toLocal = this.transform.worldToLocalMatrix;

        // MeshFilter + MeshRenderer
        var meshFilters = GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null) continue;

            var mats = mr.sharedMaterials;
            var mesh = mf.sharedMesh;

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var ci = new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = s,
                    transform = toLocal * mf.transform.localToWorldMatrix
                };
                combineList.Add(ci);
                materials.Add(s < mats.Length ? mats[s] : mats[0]);
            }
        }

        // SkinnedMeshRenderer (bake)
        var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in skinnedRenderers)
        {
            if (smr.sharedMesh == null) continue;
            var mats = smr.sharedMaterials;

            var baked = new Mesh();
            smr.BakeMesh(baked);
            tempBakedMeshes.Add(baked);

            for (int s = 0; s < baked.subMeshCount; s++)
            {
                var ci = new CombineInstance
                {
                    mesh = baked,
                    subMeshIndex = s,
                    transform = toLocal * smr.transform.localToWorldMatrix
                };
                combineList.Add(ci);
                materials.Add(s < mats.Length ? mats[s] : (mats.Length > 0 ? mats[0] : null));
            }
        }
        
        if (combineList.Count == 0)
        {
            var singleMf = GetComponentInChildren<MeshFilter>();
            var singleMr = singleMf != null ? singleMf.GetComponent<MeshRenderer>() : null;
            if (singleMf != null && singleMf.sharedMesh != null && singleMr != null)
            {
                var meshFilter = _cloneObject.AddComponent<MeshFilter>();
                var meshRenderer = _cloneObject.AddComponent<MeshRenderer>();
                meshFilter.sharedMesh = singleMf.sharedMesh;
                meshRenderer.sharedMaterials = singleMr.sharedMaterials;
                // Ajustar el mesh al espacio local del objeto: hacer que la malla quede centrada en el origen del objeto
                _cloneObject.transform.localScale = transform.localScale;
            }
        }
        else
        {
            var combined = new Mesh();
            combined.name = $"{name}_Combined";
            combined.CombineMeshes(combineList.ToArray(), false, true);
            combined.RecalculateBounds();
            combined.RecalculateNormals();

            var meshFilter = _cloneObject.AddComponent<MeshFilter>();
            var meshRenderer = _cloneObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = combined;
            meshRenderer.sharedMaterials = materials.ToArray();

            _cloneObject.transform.localScale = transform.localScale;

            // limpiar meshes horneados temporales
            foreach (var m in tempBakedMeshes)
            {
                Destroy(m);
            }
            tempBakedMeshes.Clear();
        }

        // No tener scripts/rigidbodies asociados
        var rb = _cloneObject.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
    }

    private void LateUpdate()
    {
        if (_inPortal == null || _outPortal == null)
        {
            if (_cloneObject != null)
            {
                _cloneObject.SetActive(false);
            }
            return;
        }

        if (_cloneObject != null && _cloneObject.activeSelf && _inPortal.IsPlaced && _outPortal.IsPlaced)
        {
            UpdateCloneTransform();
        }
        else if (_cloneObject != null)
        {
            _cloneObject.SetActive(false);
        }
    }
    
    private void UpdateCloneTransform()
    {
        if (_inPortal == null || _outPortal == null || _cloneObject == null) return;

        var inT = _inPortal.transform;
        var outT = _outPortal.transform;

        // Posición relativa y rotación (misma lógica que Warp)
        Vector3 relativePos = inT.InverseTransformPoint(transform.position);
        relativePos = HalfTurn * relativePos;
        _cloneObject.transform.position = outT.TransformPoint(relativePos);

        Quaternion relativeRot = Quaternion.Inverse(inT.rotation) * transform.rotation;
        relativeRot = HalfTurn * relativeRot;
        _cloneObject.transform.rotation = outT.rotation * relativeRot;

        // Mantener la escala relativa
        _cloneObject.transform.localScale = transform.localScale;
    }

    // Usado por Portal para notificar que este objeto está entrando en el portal
    public void SetIsInPortal(Portal inPortal, Portal outPortal)
    {
        _inPortal = inPortal;
        _outPortal = outPortal;

        if (_inPortal?.WallCollider != null)
            Physics.IgnoreCollision(_collider, _inPortal.WallCollider, true);
        if (_outPortal?.WallCollider != null)
            Physics.IgnoreCollision(_collider, _outPortal.WallCollider, true);

        if (_inPortal?.PortalCollider != null)
            _inPortal.PortalCollider.SetActive(true);
        if (_outPortal?.PortalCollider != null)
            _outPortal.PortalCollider.SetActive(true);

        if (_cloneObject != null)
        {
            _cloneObject.SetActive(true);
            // Posicionar inmediatamente el clon para evitar parpadeos
            UpdateCloneTransform();
        }

        ++_inPortalCount;
    }

    // Salir del portal: revertir estados
    public void ExitPortal()
    {
        if (_inPortal?.WallCollider != null)
            Physics.IgnoreCollision(_collider, _inPortal.WallCollider, false);
        if (_outPortal?.WallCollider != null)
            Physics.IgnoreCollision(_collider, _outPortal.WallCollider, false);

        if (_inPortal?.PortalCollider != null)
            _inPortal.PortalCollider.SetActive(false);
        if (_outPortal?.PortalCollider != null)
            _outPortal.PortalCollider.SetActive(false);

        _inPortal = null;
        _outPortal = null;

        if (_cloneObject != null)
            _cloneObject.SetActive(false);

        _inPortalCount = 0;
    }

    // Realiza la teleportación (warp) aplicando rotación/pos/velocidad
    public virtual void Warp()
    {
        if (_inPortal == null || _outPortal == null || _rigidbody == null) return;

        var inT = _inPortal.transform;
        var outT = _outPortal.transform;

        // Posición
        Vector3 relativePos = inT.InverseTransformPoint(transform.position);
        relativePos = HalfTurn * relativePos;
        transform.position = outT.TransformPoint(relativePos);

        // Rotación
        Quaternion relativeRot = Quaternion.Inverse(inT.rotation) * transform.rotation;
        relativeRot = HalfTurn * relativeRot;
        transform.rotation = outT.rotation * relativeRot;

        // Velocidad: usar velocity y aplicar tope vertical igual que PlayerMotor,
        // además aplicar el mismo "boost" vertical que PortalableCharacter.
        Vector3 inVel = _rigidbody.linearVelocity;
        inVel.y = Mathf.Max(inVel.y, MAX_FALL_SPEED);

        Vector3 relVel = inT.InverseTransformDirection(inVel);
        relVel = HalfTurn * relVel;
        Vector3 outVel = outT.TransformDirection(relVel);

        float upAlign = Vector3.Dot(outT.forward.normalized, Vector3.up);
        if (upAlign > 0f)
        {
            float speedMag = inVel.magnitude;
            float boost = Mathf.Lerp(2f, 8f, upAlign) + 0.25f * speedMag;
            // Coincide con PortalableCharacter: restar impulso en la dirección forward del portal de salida
            outVel -= outT.forward * boost;
        }

        _rigidbody.linearVelocity = outVel;

        // Intercambiar referencias de portal
        var tmp = _inPortal;
        _inPortal = _outPortal;
        _outPortal = tmp;

        // Desactivar clon porque ahora el objeto está del otro lado
        if (_cloneObject != null)
            _cloneObject.SetActive(false);

        _inPortalCount = Mathf.Max(0, _inPortalCount - 1);
    }

    // Utilidad para comprobar si el objeto ha cruzado el plano del portal
    public bool HasCrossedPlane(Portal portal)
    {
        if (portal == null) return false;
        Vector3 local = portal.transform.InverseTransformPoint(transform.position);
        return local.z > 0f;
    }
}
