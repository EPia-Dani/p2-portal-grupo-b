using _Scripts.Interfaces;
using _Scripts.Player.Runtime;
using UnityEngine;

namespace _Scripts.Extras
{
    public class Checkpoint : MonoBehaviour, ICheckpoint, IInteractable
    {
        
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private GameObject indicatorObject;
        [SerializeField] private GameObject activationEffect;
        private bool activated;
        
        void Start()
        {
            if(indicatorObject != null)
                indicatorObject.SetActive(false);
            if(activationEffect != null)
                activationEffect.SetActive(false);
        }
        
        public void Activate()
        {
            //GameManager.Instance.SetActiveCheckpoint(this);
            activationEffect.SetActive(true);
            activated = true;
        }

        public void Deactivate()
        {
            activationEffect.SetActive(false);
            activated = false;
        }

        public Vector3 GetSpawnPosition()
        {
            return spawnPoint != null ? spawnPoint.position : transform.position;
        }
        public void OnLookEnter(Transform viewer, RaycastHit hit)
        {
            if(activated) return;
            indicatorObject.SetActive(true);
        }

        public void Interact(PlayerInteractionController by)
        {
            Activate();
        }

        public void OnLookExit(Transform viewer)
        {
            indicatorObject.SetActive(false);
        }
    }
}