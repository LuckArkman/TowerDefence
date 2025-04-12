using System;
using UnityEngine;

namespace TowerDefence.Scripts
{
    [Serializable]
    public class ProgressionManager
    {
        public ProgressionManager(){}
        public Progression _progression;
        public ProgressionManager(Progression progression)
            => _progression = progression;

        public bool OnLevel;
        
        public void AddExperience(long amount)
        {
            _progression.currentExperience += amount;
            if (_progression.currentExperience >= _progression.experienceToNextLevel) LevelUp();
        }

        // Subir de nível
        private void LevelUp()
        {
            
            Debug.Log("LevelUP");
            _progression.currentExperience -= _progression.experienceToNextLevel;
            _progression.level++;
            _progression.CalcExperience();
        }
    }
}