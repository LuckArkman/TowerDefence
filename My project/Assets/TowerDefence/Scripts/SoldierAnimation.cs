using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefence.Scripts
{
    public class SoldierAnimation : MonoBehaviour
    {
        public List<SpriteController> _sprites = new ();
        public SpriteRenderer _spriteRenderer;
        public int x = 0;
        private void Start()
        {
            InvokeRepeating("Animation", 0.1f, 0.05f);
        }

        private void Animation()
        {
            if (x >= _sprites.Count) x = 0;
            if (!_sprites[x].action)
            {
                _spriteRenderer.sprite = _sprites[x]._Sprite;
            }
            if (x < _sprites.Count) x++;
        }
    }
}