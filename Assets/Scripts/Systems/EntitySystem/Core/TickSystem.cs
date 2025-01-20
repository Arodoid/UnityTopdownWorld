using UnityEngine;
using System.Collections.Generic;

namespace EntitySystem.Core
{
    public class TickSystem : MonoBehaviour
    {
        [SerializeField] private float _tickRate = 0.1f; // 100ms between ticks
        private float _tickTimer;
        private HashSet<ITickable> _tickables = new();

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= _tickRate)
            {
                _tickTimer -= _tickRate;
                ExecuteTick();
            }
        }

        private void ExecuteTick()
        {
            foreach (var tickable in _tickables)
            {
                tickable.OnTick();
            }
        }

        public void Register(ITickable tickable)
        {
            _tickables.Add(tickable);
        }

        public void Unregister(ITickable tickable)
        {
            _tickables.Remove(tickable);
        }
    }
} 