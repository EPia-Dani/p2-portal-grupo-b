using _Scripts.Interfaces;
using UnityEngine;

namespace _Scripts.RefractionCube
{
    [DisallowMultipleComponent]
    public class RefractionCube : MonoBehaviour, ILaserRedirector
    {
        [SerializeField] private bool visualizeForward = true;
        [SerializeField] private Color gizmoColor = new(0.2f, 1f, 0.6f, 0.9f);
        
        public bool TryRedirect(Ray inRay, RaycastHit hit, out Ray outRay)
        {
            Vector3 dir = transform.forward.normalized;
            outRay = new Ray(transform.position, dir);
            return true;
        }

        void OnDrawGizmos()
        {
            if (!visualizeForward) return;
            Gizmos.color = gizmoColor;
            Vector3 p = transform.position;
            Vector3 f = transform.forward;
            Gizmos.DrawLine(p, p + f * 0.8f);
            Gizmos.DrawSphere(p + f * 0.8f, 0.03f);
        }
    }
}