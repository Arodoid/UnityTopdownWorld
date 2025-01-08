using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;
using WorldSystem.Data;

namespace WorldSystem.Base
{
    public class ChunkPool
    {
        private Queue<GameObject> _pool;
        private Dictionary<int2, (float timestamp, GameObject gameObject)> _inactiveChunks;
        private Dictionary<int2, GameObject> _activeChunks;
        
        private readonly Material _chunkMaterial;
        private readonly Transform _parentTransform;
        private readonly int _poolSize;
        private readonly int _maxChunks;
        private readonly float _bufferTimeSeconds;
        
        private float _lastBufferCheck;
        private const float BUFFER_CHECK_INTERVAL = 1f;

        public int TotalChunksInUse => _activeChunks.Count + _inactiveChunks.Count + _pool.Count;
        public bool HasAvailableChunk => _pool.Count > 0 || TotalChunksInUse < _maxChunks;

        public ChunkPool(Material chunkMaterial, Transform parentTransform, int poolSize, int maxChunks, float bufferTimeSeconds)
        {
            _chunkMaterial = chunkMaterial;
            _parentTransform = parentTransform;
            _poolSize = poolSize;
            _maxChunks = maxChunks;
            _bufferTimeSeconds = bufferTimeSeconds;
            
            _pool = new Queue<GameObject>();
            _inactiveChunks = new Dictionary<int2, (float timestamp, GameObject gameObject)>();
            _activeChunks = new Dictionary<int2, GameObject>();
            
            InitializePool();
        }

        private void InitializePool()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                var chunk = CreateChunkGameObject();
                chunk.SetActive(false);
                _pool.Enqueue(chunk);
            }
        }

        private GameObject CreateChunkGameObject()
        {
            var chunk = new GameObject("Chunk");
            chunk.transform.parent = _parentTransform;
            return chunk;
        }

        private GameObject CreateYLevelMesh(GameObject parentChunk, int yLevel)
        {
            var yLevelObject = new GameObject($"Y_Level_{yLevel}");
            yLevelObject.transform.parent = parentChunk.transform;
            yLevelObject.transform.localPosition = Vector3.zero;
            
            // Add components for render mesh
            yLevelObject.AddComponent<MeshFilter>();
            yLevelObject.AddComponent<MeshRenderer>().material = _chunkMaterial;
            
            // Add components for shadow mesh
            var shadowObject = new GameObject("Shadow Mesh");
            shadowObject.transform.parent = yLevelObject.transform;
            shadowObject.transform.localPosition = Vector3.zero;
            var shadowMeshFilter = shadowObject.AddComponent<MeshFilter>();
            var shadowMeshRenderer = shadowObject.AddComponent<MeshRenderer>();
            shadowMeshRenderer.material = _chunkMaterial;
            shadowMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            shadowMeshRenderer.receiveShadows = false;
            
            return yLevelObject;
        }

        public (GameObject chunk, MeshFilter meshFilter, MeshFilter shadowFilter)? GetChunk(int2 position, int yLevel)
        {
            GameObject chunkObject;
            GameObject yLevelObject;
            
            // First check if we have an active or inactive chunk at this position
            if (_activeChunks.TryGetValue(position, out chunkObject))
            {
                // Chunk is already active
            }
            else if (_inactiveChunks.TryGetValue(position, out var inactiveData))
            {
                // If chunk was inactive, reactivate it
                chunkObject = inactiveData.gameObject;
                _inactiveChunks.Remove(position);
                _activeChunks[position] = chunkObject;
                chunkObject.SetActive(true);
            }
            else if (_pool.Count > 0)
            {
                chunkObject = _pool.Dequeue();
                chunkObject.SetActive(true);
                _activeChunks[position] = chunkObject;
            }
            else if (TotalChunksInUse >= _maxChunks)
            {
                ForceReclaimOldestChunks();
                if (_pool.Count == 0) return null;
                
                chunkObject = _pool.Dequeue();
                chunkObject.SetActive(true);
                _activeChunks[position] = chunkObject;
            }
            else
            {
                return null;
            }

            // Modified Y-level handling - don't deactivate other levels immediately
            foreach (Transform child in chunkObject.transform)
            {
                if (child.name.StartsWith("Y_Level_") && child.name != $"Y_Level_{yLevel}")
                {
                    // Removed the immediate deactivation
                    continue;
                }
            }

            // Try to find or create the requested Y-level
            yLevelObject = FindYLevelMesh(chunkObject, yLevel);
            if (yLevelObject == null)
            {
                yLevelObject = CreateYLevelMesh(chunkObject, yLevel);
            }
            yLevelObject.SetActive(true);

            // Now deactivate other Y-levels only if this one is fully set up
            foreach (Transform child in chunkObject.transform)
            {
                if (child.name.StartsWith("Y_Level_") && child.name != $"Y_Level_{yLevel}")
                {
                    child.gameObject.SetActive(false);
                }
            }

            chunkObject.name = $"Chunk {position.x},{position.y}";
            chunkObject.transform.position = new Vector3(position.x * ChunkData.SIZE, 0, position.y * ChunkData.SIZE);

            var meshFilter = yLevelObject.GetComponent<MeshFilter>();
            var shadowMeshFilter = yLevelObject.transform.GetChild(0).GetComponent<MeshFilter>();
            
            return (chunkObject, meshFilter, shadowMeshFilter);
        }

        private GameObject FindYLevelMesh(GameObject chunkObject, int yLevel)
        {
            return chunkObject.transform.Find($"Y_Level_{yLevel}")?.gameObject;
        }

        private void ForceReclaimOldestChunks()
        {
            var oldestChunks = _inactiveChunks
                .OrderBy(x => x.Value.timestamp)
                .Take(_maxChunks / 4) // Reclaim 25% of max chunks
                .ToList();

            foreach (var old in oldestChunks)
            {
                _pool.Enqueue(old.Value.gameObject);
                _inactiveChunks.Remove(old.Key);
            }
        }

        public void DeactivateChunk(int2 position, int yLevel)
        {
            if (_activeChunks.TryGetValue(position, out var chunk))
            {
                var yLevelObject = FindYLevelMesh(chunk, yLevel);
                if (yLevelObject != null)
                {
                    yLevelObject.SetActive(false);
                }

                // If no Y-level meshes are active, deactivate the whole chunk
                if (!HasAnyActiveYLevels(chunk))
                {
                    _inactiveChunks[position] = (Time.time, chunk);
                    chunk.SetActive(false);
                    _activeChunks.Remove(position);
                }
            }
        }

        private bool HasAnyActiveYLevels(GameObject chunk)
        {
            foreach (Transform child in chunk.transform)
            {
                if (child.gameObject.activeSelf && child.name.StartsWith("Y_Level_"))
                {
                    return true;
                }
            }
            return false;
        }

        public void CheckBufferTimeout()
        {
            var currentTime = Time.time;
            if (currentTime - _lastBufferCheck <= BUFFER_CHECK_INTERVAL) return;
            
            _lastBufferCheck = currentTime;
            
            var expiredChunks = _inactiveChunks
                .Where(x => currentTime - x.Value.timestamp > _bufferTimeSeconds)
                .ToList();

            foreach (var expired in expiredChunks)
            {
                _pool.Enqueue(expired.Value.gameObject);
                _inactiveChunks.Remove(expired.Key);
            }
        }

        public void Cleanup()
        {
            foreach (var chunk in _activeChunks.Values)
            {
                if (chunk != null)
                    Object.Destroy(chunk);
            }
            
            foreach (var inactive in _inactiveChunks.Values)
            {
                if (inactive.gameObject != null)
                    Object.Destroy(inactive.gameObject);
            }
            
            while (_pool.Count > 0)
            {
                var chunk = _pool.Dequeue();
                if (chunk != null)
                    Object.Destroy(chunk);
            }
            
            _activeChunks.Clear();
            _inactiveChunks.Clear();
        }

        public bool HasChunkAtPosition(int2 position, int yLevel)
        {
            return _activeChunks.ContainsKey(position) || _inactiveChunks.ContainsKey(position);
        }

        public IEnumerable<(int2 position, int yLevel)> GetActiveChunkPositions()
        {
            // Convert dictionary keys to tuples with y-levels
            return _activeChunks.SelectMany(chunk => 
                chunk.Value.transform.GetComponentsInChildren<Transform>()
                    .Where(t => t.name.StartsWith("Y_Level_"))
                    .Select(t => (
                        position: chunk.Key,
                        yLevel: int.Parse(t.name.Replace("Y_Level_", ""))
                    ))
            );
        }

        public bool HasActiveChunkAtPosition(int2 position, int yLevel)
        {
            if (!_activeChunks.TryGetValue(position, out var chunk))
                return false;
            
            return FindYLevelMesh(chunk, yLevel) != null;
        }
    }
} 