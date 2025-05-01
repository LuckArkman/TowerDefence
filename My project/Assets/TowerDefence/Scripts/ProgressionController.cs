using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Scripts
{
    public class ProgressionController : MonoBehaviour
    {
        public TextMeshProUGUI _points,_wave,_deadMonsters, _towers, _level;
        public Image _filled;
        private void LateUpdate()
        {
            _level.text = $"{Singleton._Instance.progression.level}";
            _points.text = $"points : {Singleton._Instance._points}";
            _wave.text = $"waves : {Singleton._Instance._wave}";
            _deadMonsters.text = $"kills : {Singleton._Instance._deadMonsters}";
            _towers.text = $"towers : 8/8";
            _filled.fillAmount = Singleton._Instance.progression.GetParcent();
        }
    }
}