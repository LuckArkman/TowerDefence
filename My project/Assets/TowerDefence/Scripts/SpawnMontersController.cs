

using System;
using System.Collections;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using Random = System.Random;

namespace TowerDefence.Scripts
{
    public class SpawnMontersController : MonoBehaviour
    {
        public ZombieAnimation _zombieAnimation;
        public TextMeshProUGUI _wave, _monsterNumber, _points;
        public int maxSpawn;
        
        public Circunferencia2D circunferencia;

        public WaveController waveController;
        private void Start()
        {
            InvokeRepeating("WaveProgress", 5.0f, 30.0f);
        }

        private void WaveProgress()
        {
            _wave.text = $"Wave : {waveController.waveNumber}";
            int x = 0;
            if (waveController.totalSpawn < waveController.SpawnNumber)
            {
                Random rn = new Random();
                bool value = new Random().NextDouble() <= (10 / 100);
                if(value)x = rn.Next(0, maxSpawn * 2);
                if(!value)x = rn.Next(0, maxSpawn);
                for (int i = 0; i < x; i++)
                {
                    int p = UnityEngine.Random.Range(0, circunferencia.spawnpoints.Count);
                    Vector3 position = circunferencia.spawnpoints[p];
                    var monster = Instantiate(_zombieAnimation, position, Quaternion.identity);
                    Singleton._Instance._zombieAnimations.Add(monster);
                }
                waveController.totalSpawn += x;
                _monsterNumber.text = $"Monters : {waveController.totalSpawn} / {waveController.SpawnNumber}";
            }
        }
    }
}