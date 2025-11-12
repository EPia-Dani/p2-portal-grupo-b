using UnityEngine;

namespace _Scripts.Interfaces
{
    public interface ILaserReceiver
    {
        void LaserHit(Vector3 point, Vector3 normal, int frame);
    }
}
