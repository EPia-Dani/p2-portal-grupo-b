using _Scripts.Interfaces;
using UnityEngine;

namespace _Scripts.Extras
{
    [RequireComponent(typeof(Collider))]
    public class DeadZone : MonoBehaviour
    {
        private Collider _deadZoneCollider;
        
        private void Awake()
        {
            _deadZoneCollider = GetComponent<Collider>();
            if (!_deadZoneCollider.isTrigger)
            {
                Debug.LogWarning("DeadZone collider should be set as Trigger. Setting it automatically.");
                _deadZoneCollider.isTrigger = true;
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log("DeadZone triggered by: " + other.name);
            other.GetComponent<ICanDie>()?.Die();
        }
    }
}
