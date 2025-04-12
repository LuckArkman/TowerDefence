using System.Collections.Generic;
using UnityEngine;

namespace TowerDefence.Scripts
{
    public class Singleton : MonoBehaviour
    {
        public int maxLevel;
        public List<ProgressaoAvalible> progressaoAvalibles = new ();
        public float experienceMultiplier = 1.5f;
        public Circunferencia2D circunferencia;
        public int _points, _wave, _deadMonsters;
        public Progression progression = new ();
        public List<ZombieAnimation> _zombieAnimations = new ();
        private static Singleton instance;

        [ContextMenu(nameof(OnTeste))]
        public void OnTeste()
        {
            for (int i = progressaoAvalibles.Count; i < maxLevel; i++)
            {
                Debug.Log(i);
                if (progressaoAvalibles.Count > 0)
                {
                    progressaoAvalibles.Add(new ProgressaoAvalible(
                        Mathf.RoundToInt(progressaoAvalibles[i -1 ].xp * experienceMultiplier)));
                }
                if (progressaoAvalibles.Count <= 0)
                {
                    progressaoAvalibles.Add(new ProgressaoAvalible(100));
                }
            }
        }

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