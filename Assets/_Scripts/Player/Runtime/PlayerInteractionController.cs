using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Player.Runtime
{
    public class PlayerInteractionController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Camera playerCam;
        
        public void OnInteract(InputAction.CallbackContext ctx)
        {

            if (ctx.started)
                TryInteract();
        }
        
        void TryInteract()
        {
            var detector = playerCam.GetComponent<PlayerLookDetector>();
            var interactable = detector?.CurrentLook; // look for IInteractable in front of player
            if (interactable != null)
            {
                interactable.Interact(this);
            }
        }
        
    }
}