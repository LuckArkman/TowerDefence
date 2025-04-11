using System;
using System.Collections;
using UnityEngine;
using Random = System.Random;

namespace TowerDefence.Scripts
{
    public class Bullet : MonoBehaviour
    {
        public ZombieAnimation target;       // O alvo a ser seguido
        public float speed;
        public Rigidbody2D rb;
        
        void Start()
        {
            rb.gravityScale = 0f;
            InvokeRepeating("Verify", 0.1f, 1.0f);
        }

        private void Verify()
        {
            var obj = Singleton._Instance._zombieAnimations.Find(x => x.id == target.id);
            if(obj == null) Destroy(this.gameObject);
        }

        void FixedUpdate()
        {
            if (target == null) return;

            // Calcula a direção normalizada
            Vector2 direction = (target.transform.position - transform.position).normalized;

            // Move na direção do alvo
            rb.linearVelocity = direction * speed;
            if (target != null && Vector3.Distance(transform.position, target.transform.position) <= 0.05f)
            {
                target._health.TakeDamage(5);
                if (!target._health.IsAlive())
                {
                    var rn = new Random();
                    Singleton._Instance.progression.AddXp(rn.Next(0,5));
                    Destroy(target.gameObject);
                }
                Destroy(this.gameObject);
            }
        }
    }
}