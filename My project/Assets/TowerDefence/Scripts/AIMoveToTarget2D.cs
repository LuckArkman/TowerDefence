using UnityEngine;

namespace TowerDefence.Scripts
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class AIMoveToTarget2D : MonoBehaviour
    {
        public Transform target;       // O alvo a ser seguido
        public float speed = 2f;       // Velocidade da AI

        private Rigidbody2D rb;

        void Start()
        {
            target = Singleton._Instance.circunferencia.transform;
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
        }

        void Update()
        {
            if (target == null) return;

            // Calcula a direção normalizada
            Vector2 direction = (target.position - transform.position).normalized;

            // Move na direção do alvo
            rb.linearVelocity = direction * speed;
        }
    }
}