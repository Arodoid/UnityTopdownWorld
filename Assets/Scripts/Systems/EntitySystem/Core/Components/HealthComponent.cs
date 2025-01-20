using UnityEngine;
using System;

namespace EntitySystem.Core.Components
{
    public class HealthComponent : EntityComponent
    {
        [SerializeField] public float _maxHealth = 100f;
        private float _currentHealth;

        public event Action<float> OnHealthChanged;
        public event Action OnDeath;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsDead => _currentHealth <= 0;

        protected override void OnInitialize(EntityManager entityManager)
        {
            _currentHealth = _maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            OnHealthChanged?.Invoke(_currentHealth);

            if (IsDead)
            {
                OnDeath?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth);
        }
    }
} 