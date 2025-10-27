using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DoorSensor : MonoBehaviour
{
    
    [SerializeField] private DoorBase door;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            door.Open();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            door.Close();
        }
    }
}
