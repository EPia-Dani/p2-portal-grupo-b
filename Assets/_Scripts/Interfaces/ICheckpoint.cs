using UnityEngine;

namespace _Scripts.Interfaces
{
    public interface ICheckpoint
    {
        void Deactivate();
        Vector3 GetSpawnPosition();
    }
}