using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Scripts
{
    public class ProgressionController : MonoBehaviour
    {
        public TextMeshProUGUI _points, _level;
        public Image _filled;
        private void LateUpdate()
        {
            _level.text = $"{Singleton._Instance.progression.level}";
            _points.text = $"{Singleton._Instance._points}";
            _filled.fillAmount = Singleton._Instance.progression.GetParcent();
        }
    }
}