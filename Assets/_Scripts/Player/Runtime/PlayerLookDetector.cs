using UnityEngine;
using _Scripts.Interfaces;
using _Scripts.Player;

namespace _Scripts.Player.Runtime
{
    [RequireComponent(typeof(Camera))]
    public class PlayerLookDetector : MonoBehaviour
    {
        [SerializeField] private float maxDistance = 3f;
        [SerializeField] private LayerMask lookMask = ~0;
        private IInteractable _current;
        private Camera _cam;

        public IInteractable CurrentLook => _current;

        void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        void Update()
        {
            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, maxDistance, lookMask))
            {
                var lookable = hit.collider.GetComponentInParent<IInteractable>();
                if (!ReferenceEquals(lookable, _current))
                {
                    _current?.OnLookExit(_cam.transform);
                    _current = lookable;
                    _current?.OnLookEnter(_cam.transform, hit);
                }
            }
            else
            {
                if (_current != null && _cam != null)
                {
                    _current.OnLookExit(_cam.transform);
                    _current = null;
                }
            }
        }
    }

}