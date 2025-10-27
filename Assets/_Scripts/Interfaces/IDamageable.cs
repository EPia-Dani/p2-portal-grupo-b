using System;
using UnityEngine;
namespace _Scripts.Interfaces
{
    public interface IDamageable
    {
        static Action OnHitTaken;
        void ApplyDamage(int amount);
    }

}