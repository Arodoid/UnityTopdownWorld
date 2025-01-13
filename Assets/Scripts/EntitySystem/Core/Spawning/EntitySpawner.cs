using UnityEngine;
using EntitySystem.Core;

namespace EntitySystem.Core.Spawning
{
    public abstract class EntitySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] protected float spawnRadius = 10f;
        [SerializeField] protected int maxEntities = 5;
        [SerializeField] protected float spawnInterval = 2f;
        
        protected EntityManager EntityManager { get; private set; }
        protected bool IsInitialized { get; private set; }
        
        // Initialization state
        private bool _isEnabled;
        private bool _isSystemReady;

        public virtual void Initialize(EntityManager entityManager)
        {
            EntityManager = entityManager;
            _isSystemReady = true;
            
            OnSystemInitialized();
            
            // If we were enabled before initialization, start now
            if (_isEnabled)
            {
                BeginSpawning();
            }
        }

        protected virtual void OnEnable()
        {
            _isEnabled = true;
            if (_isSystemReady)
            {
                BeginSpawning();
            }
        }

        protected virtual void OnDisable()
        {
            _isEnabled = false;
            EndSpawning();
        }

        protected void BeginSpawning()
        {
            IsInitialized = true;
            OnBeginSpawning();
        }

        protected void EndSpawning()
        {
            OnEndSpawning();
        }

        // Override these in derived classes
        protected virtual void OnSystemInitialized() { }
        protected abstract void OnBeginSpawning();
        protected abstract void OnEndSpawning();

        protected Vector3 GetRandomSpawnPosition()
        {
            var randomAngle = UnityEngine.Random.Range(0f, 360f);
            var randomDistance = UnityEngine.Random.Range(0f, spawnRadius);
            var offset = Quaternion.Euler(0, randomAngle, 0) * Vector3.forward * randomDistance;
            return transform.position + offset;
        }
    }
}