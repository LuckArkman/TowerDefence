using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Scripts
{
    [Serializable]
    public class Health
    {
        public GameObject _gameObject;
        public Image _image;
        public float maxHealth;
        public float currentHealth;

        public void Start()
        {
            currentHealth = maxHealth;
        }
        public void TakeDamage(int damage)
        {
            if (currentHealth > 0)currentHealth -= damage;
            if (currentHealth <= 0) currentHealth = 0;
            _gameObject.SetActive(true);
            _image.fillAmount = (float)currentHealth / (float)maxHealth;
        }
        public bool IsAlive() => currentHealth > 0;
        
    }
}