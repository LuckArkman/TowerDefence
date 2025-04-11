using System;
using UnityEngine;

namespace TowerDefence.Scripts
{
    [Serializable]
    public class Progression
    {
        public int level = 1; 
        public int currentExperience = 0;
        public int experienceToNextLevel = 100; 
        public float experienceMultiplier = 1.5f;
        public Progression(){}

        public void AddXp(int amount)
        {
            new ProgressionManager(this).AddExperience(amount);
        }


        public void LevelUp()
        {
            var progression = new ProgressionManager(this);
            progression.AddExperience(currentExperience);
        }
        
        public void CalcExperience()
        {
            int experienceNextLevel = 100;
            int i = 1;
            while (i <= level)
            {
                experienceNextLevel = Mathf.RoundToInt(experienceNextLevel * experienceMultiplier);
                experienceToNextLevel = experienceNextLevel;
                i++;
            }
        }

        public float GetParcent()
        =>  (float)currentExperience / (float)experienceToNextLevel;
    }
}