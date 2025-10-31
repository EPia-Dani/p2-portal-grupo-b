using UnityEngine;

public class GravityGun : MonoBehaviour
{
    [SerializeField] private float grabRange = 8f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform holdPoint; // create a child empty GameObject ~2m ahead

    private IGrabbable grabbedObject;
    
    public Transform HoldPoint => holdPoint;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (grabbedObject == null)
            {
                Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hitInfo, grabRange))
                {
                    IGrabbable grabbable = hitInfo.collider.GetComponent<IGrabbable>();
                    if (grabbable != null)
                    {
                        grabbedObject = grabbable;
                        grabbedObject?.OnGrab(this);
                    }
                }
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            grabbedObject?.OnRelease();
            grabbedObject = null;
        }
    }

    public void Release()
    {
        grabbedObject = null;
    }
}