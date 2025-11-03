using _Scripts.Interfaces;
using UnityEngine;

namespace _Scripts.Player.Runtime
{
    public class PlayerHealth : MonoBehaviour, IDamageable {
        [SerializeField] float maxHP = 100f;
        float hp;
        void Awake(){ hp = maxHP; }
        public void ApplyDamage(float dmg, Vector3 p, Vector3 n){
            hp -= dmg;
            Debug.Log($"Player took {dmg} damage, HP: {hp}/{maxHP}");
            if (hp <= 0f)
            {
                Debug.Log("Player Died");
            }
        }
    }
}