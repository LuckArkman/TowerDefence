using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TowerDefence.Scripts
{
    public class SoldierManager : MonoBehaviour
    {
        public ZombieAnimation target;
        public Bullet bullet;
        public List<ZombieAnimation> _enemys = new ();
        private void Start()
        {
            InvokeRepeating("LoadTargetMonsters", 5.0f, 1.0f);
        }

        private void LoadTargetMonsters()
        {
            _enemys.Clear();
            Singleton._Instance._zombieAnimations.ForEach(z =>
            {
                if (z != null)
                {
                    if (Vector3.Distance(z.transform.position, transform.position) < 1.25f)
                    {
                        _enemys.Add(z);
                    }
                }
            });
            target = GetNearestEnemyGameObject();
            if (target != null)
            {
                var _projetil = Instantiate(bullet, transform.position, Quaternion.identity);
                _projetil.target = target;
            }
        }


        public ZombieAnimation GetNearestEnemyGameObject()
        {
            if (_enemys.Count > 0)
            {
                var nearestEnemy = _enemys
                    .OrderBy(enemy =>
                    {
                        return Vector3.Distance(this.transform.position, enemy.transform.position);
                    })
                    .FirstOrDefault();

                return nearestEnemy != null ? nearestEnemy : null;
            }
            return null;
        }
    }
}