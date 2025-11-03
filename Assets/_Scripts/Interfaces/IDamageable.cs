using System;
using UnityEngine;
namespace _Scripts.Interfaces
{
    public interface IDamageable
    {
        void ApplyDamage(float dmg, Vector3 hitPoint, Vector3 hitNormal);
    }
}