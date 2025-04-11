using System.Collections.Generic;
using UnityEngine;

namespace TowerDefence.Scripts
{
    public class Singleton : MonoBehaviour
    {
        public Circunferencia2D circunferencia;
        public int _points;
        public Progression progression = new ();
        public List<ZombieAnimation> _zombieAnimations = new ();
        private static Singleton instance;

        public static Singleton _Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<Singleton>();
                    if (instance == null)
                    {
                        GameObject singletonObject = new GameObject("Singleton");
                        instance = singletonObject.AddComponent<Singleton>();
                    }
                }

                return instance;
            }
        }

        private void Start()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }

    }
}