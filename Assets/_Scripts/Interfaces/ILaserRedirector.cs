using UnityEngine;

namespace _Scripts.Interfaces
{
    public interface ILaserRedirector
    {
        bool TryRedirect(Ray inRay, RaycastHit hit, out Ray outRay);
    }
}
