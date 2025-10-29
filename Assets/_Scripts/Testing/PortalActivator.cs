using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

public class PortalActivator : MonoBehaviour
{
    private VisualEffect vfx;
    
    private void Awake()
    {
        vfx = GetComponentInChildren<VisualEffect>();
    }
    
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            vfx.SendEvent("Activate");
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            vfx.SendEvent("Deactivate");
        }
    }
    
}
