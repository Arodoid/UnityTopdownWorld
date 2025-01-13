using UnityEngine;
using System;

namespace EntitySystem.Core
{
    public class TickManager : MonoBehaviour
    {
        [SerializeField] private float tickInterval = 0.1f;
        private float _nextTickTime;
        
        public event Action OnTick;

        private void Update()
        {
            if (Time.time >= _nextTickTime)
            {
                OnTick?.Invoke();
                _nextTickTime = Time.time + tickInterval;
            }
        }
    }
} 