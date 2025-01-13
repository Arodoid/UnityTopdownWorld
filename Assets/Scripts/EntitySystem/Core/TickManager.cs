using UnityEngine;
using System;
using System.Collections.Generic;

namespace EntitySystem.Core
{
    public class TickManager : MonoBehaviour
    {
        public const int TICKS_PER_SECOND = 20;
        public const float TICK_INTERVAL = 1f / TICKS_PER_SECOND;

        private float _accumulator;
        private int _currentTick;
        
        public event Action OnTick;
        public event Action OnSlowTick; // Every 4 ticks
        
        private void Update()
        {
            _accumulator += Time.deltaTime;
            
            while (_accumulator >= TICK_INTERVAL)
            {
                ExecuteTick();
                _accumulator -= TICK_INTERVAL;
            }
        }

        private void ExecuteTick()
        {
            _currentTick++;
            OnTick?.Invoke();
            
            if (_currentTick % 4 == 0)
            {
                OnSlowTick?.Invoke();
            }
        }

        public float GetTickProgress()
        {
            return _accumulator / TICK_INTERVAL;
        }
    }
} 