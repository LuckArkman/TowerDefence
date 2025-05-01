using System;

namespace TowerDefence.Scripts
{
    [Serializable]
    public class ProgressaoAvalible
    {
        public long xp;

        public ProgressaoAvalible(long xp)
        {
            this.xp = xp;
        }
    }
}