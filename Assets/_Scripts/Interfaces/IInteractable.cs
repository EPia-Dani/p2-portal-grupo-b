using UnityEngine;
using _Scripts.Player.Runtime;

namespace _Scripts.Interfaces
{
    public interface IInteractable
    {
        void OnLookEnter(Transform viewer, RaycastHit hit);
        void Interact(PlayerInteractionController by);
        void OnLookExit(Transform viewer);
    }
}