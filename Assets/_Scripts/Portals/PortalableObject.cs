using System.Collections.Generic;
using _Scripts.Interfaces;
using _Scripts.Portals;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PortalableObject : PortalableBase
{
    GameObject _clone;
    Rigidbody _rb;
    Collider _col;
    RigidbodyKinematics _kin;

    protected void Awake()
    {
        _rb  = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        _kin = new RigidbodyKinematics(_rb);
        
        _clone = BuildVisualClone();
        _clone.SetActive(false);
    }

    protected override IPortalKinematics Kin => _kin;
    protected override Collider GetMainCollider() => _col;

    void LateUpdate()
    {
        if (inPortal && outPortal) UpdateCloneTransform(); else if (_clone) _clone.SetActive(false);
    }

    protected override void OnEnterPortal()
    {
        if (_clone) { _clone.SetActive(true); UpdateCloneTransform(); }
    }

    protected override void OnExitPortal()
    {
        if (_clone) _clone.SetActive(false);
    }

    void UpdateCloneTransform()
    {
        var inT  = inPortal.transform;
        var outT = outPortal.transform;

        Vector3 relPos = inT.InverseTransformPoint(transform.position);
        relPos = HalfTurn * relPos;
        _clone.transform.position = outT.TransformPoint(relPos);

        Quaternion relRot = Quaternion.Inverse(inT.rotation) * transform.rotation;
        relRot = HalfTurn * relRot;
        _clone.transform.rotation = outT.rotation * relRot;

        _clone.transform.localScale = transform.localScale * (outPortal.CurrentScale / inPortal.CurrentScale);
    }

    GameObject BuildVisualClone()
    {
        GameObject _cloneObject = new GameObject($"{name}_Clone");
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
        // Combinar todos los meshes en uno solo
        var combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // soportar muchos vér
        combinedMesh.CombineMeshes(combineList.ToArray(), false, true);
        // Asignar el mesh combinado al clon
        var mfClone = _cloneObject.AddComponent<MeshFilter>();
        mfClone.sharedMesh = combinedMesh;
        var mrClone = _cloneObject.AddComponent<MeshRenderer>();
        mrClone.sharedMaterials = materials.ToArray();
        // Limpiar meshes horneados temporales
        foreach (var bm in tempBakedMeshes)
            Destroy(bm);
        return _cloneObject;
    }
}
