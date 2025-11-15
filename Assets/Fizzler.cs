using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Fizzler : MonoBehaviour
{

    void OnTriggerEnter(Collider other)
    {
        if(other.GetComponent<IGrabbable>() != null)
        {
            Destroy(other.gameObject);
        }
    }
}
