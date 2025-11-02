using UnityEngine;
using UnityEngine.InputSystem;

public class GravityGun : MonoBehaviour
{
    [SerializeField] private float grabRange = 8f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform holdPoint; // create a child empty GameObject ~2m ahead

    private IGrabbable grabbedObject;
    
    public Transform HoldPoint => holdPoint;

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (grabbedObject == null)
            {
                TryPickup();
            }
            else
            {
                grabbedObject?.OnRelease();
                grabbedObject = null;
            }
        }
    }
    
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            Shoot();
        }
    }
    
    private void Shoot()
    {
        if (grabbedObject != null)
        {
            grabbedObject?.OnRelease();
            grabbedObject = null;
        }
    }
    
    private void TryPickup()
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

    public void Release()
    {
        grabbedObject = null;
    }
}