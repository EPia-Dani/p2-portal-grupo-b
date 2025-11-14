using System;
using _Scripts.Interfaces;
using UnityEngine;

namespace _Scripts.Player.Runtime
{
    public class PlayerHealth : MonoBehaviour, IDamageable, ICanDie {
        [SerializeField] float maxHP = 100f;
        float hp;
        public static Action<float> OnTakeDamage;
        private int lastDamageFrame = -1;

        void Awake(){ hp = maxHP; }

        private void Update()
        {
            if (Time.frameCount > lastDamageFrame + 30 && hp < maxHP)
            {
                hp += 10f * Time.deltaTime;
                if (hp > maxHP) hp = maxHP;
                OnTakeDamage?.Invoke(hp/maxHP);
            }
        }

        public void ApplyDamage(float dmg, Vector3 p, Vector3 n){
            hp -= dmg;
            OnTakeDamage?.Invoke(hp/maxHP);
            Debug.Log($"Player took {dmg} damage, HP: {hp}/{maxHP}");
            if (hp <= 0f)
            {
                hp = 0f;
                OnTakeDamage?.Invoke(hp/maxHP);
                Die();
            }
            lastDamageFrame = Time.frameCount;
        }

        public void Die()
        {
            GameManager.Instance?.PlayerDied();
        }
    }
}