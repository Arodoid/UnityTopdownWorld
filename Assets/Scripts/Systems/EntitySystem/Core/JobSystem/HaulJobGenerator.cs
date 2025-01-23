using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using EntitySystem.Core.Components;
using EntitySystem.Data;
using System.Linq;

namespace EntitySystem.Core.Jobs
{
    public class HaulJobGenerator : MonoBehaviour, ITickable
    {
        [SerializeField] private int _scanIntervalTicks = 1;  // Ticks between scans
        private int _ticksUntilNextScan;
        
        private JobSystemComponent _jobSystem;
        private EntityManager _entityManager;
        private TickSystem _tickSystem;
        
        private void Start()
        {
            _jobSystem = GetComponent<JobSystemComponent>();
            _entityManager = GetComponent<EntityManager>();
            _tickSystem = GetComponent<TickSystem>();
            
            if (_jobSystem == null || _entityManager == null || _tickSystem == null)
            {
                Debug.LogError("HaulJobGenerator: Missing required components!");
                enabled = false;
                return;
            }

            _tickSystem.Register(this);
            _ticksUntilNextScan = _scanIntervalTicks;
        }

        public void OnTick()
        {
            _ticksUntilNextScan--;
            if (_ticksUntilNextScan <= 0)
            {
                ScanForHaulJobs();
                _ticksUntilNextScan = _scanIntervalTicks;
            }
        }

        private void OnDestroy()
        {
            if (_tickSystem != null)
            {
                _tickSystem.Unregister(this);
            }
        }

        private void ScanForHaulJobs()
        {
            // 1. Find all items that need storage
            var itemsToStore = FindUnstaredItems()
                .Where(item => !_jobSystem.HasJobFor<HaulItemJob>(job => job.ItemToPickup.Equals(item)));
            
            // 2. Find all storage (chests for now)
            var storageSpots = FindAvailableStorage();
            
            // 3. Create haul jobs for items that can be stored
            foreach (var item in itemsToStore)
            {
                ChestComponent bestStorage = FindBestStorageFor(item, storageSpots);
                if (bestStorage != null)
                {
                    var haulJob = new HaulItemJob(item, bestStorage.GetEmptySlot().Value);
                    _jobSystem.AddGlobalJob(haulJob);
                }
            }
        }

        private List<EntityHandle> FindUnstaredItems()
        {
            var unstored = new List<EntityHandle>();
            
            foreach (var entity in _entityManager.GetAllEntities())
            {
                var itemComponent = entity.GetComponent<ItemComponent>();
                if (itemComponent == null) continue;
                
                var handle = new EntityHandle(entity.Id, entity.Version);
                
                int3 itemPos;
                if (!_entityManager.TryGetEntityPosition(entity.Id, out itemPos))
                    continue;
            
                // Check if it's at any valid chest position
                bool isStored = false;
                foreach (var potentialChest in _entityManager.GetAllEntities())
                {
                    var chest = potentialChest.GetComponent<ChestComponent>();
                    if (chest != null && chest.IsValidStoragePosition(itemPos))
                    {
                        isStored = true;
                        break;
                    }
                }
                
                if (!isStored)
                {
                    unstored.Add(handle);
                }
            }
            
            return unstored;
        }

        private List<ChestComponent> FindAvailableStorage()
        {
            var storage = new List<ChestComponent>();
            
            // Get all entities
            foreach (var entity in _entityManager.GetAllEntities())
            {
                var chest = entity.GetComponent<ChestComponent>();
                if (chest != null && chest.HasSpace())
                {
                    storage.Add(chest);
                }
            }
            
            return storage;
        }

        private ChestComponent FindBestStorageFor(EntityHandle item, List<ChestComponent> availableStorage)
        {
            if (availableStorage.Count == 0) return null;
            
            // For now, just return the first available storage
            // Later we can add distance checks and priorities
            return availableStorage[0];
        }
    }
}