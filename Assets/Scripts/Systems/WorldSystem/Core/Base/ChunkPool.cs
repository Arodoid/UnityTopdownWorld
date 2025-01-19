using UnityEngine;
using Unity.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using WorldSystem.Data;

namespace WorldSystem.Base
{
    public class ChunkPool
    {
        private Queue<GameObject> _pool;
        private Dictionary<(int2 position, int yLevel), (float timestamp, GameObject gameObject)> _inactiveChunks;
        private Dictionary<(int2 position, int yLevel), GameObject> _activeChunks;
        
        private readonly Material _chunkMaterial;
        private readonly Transform _parentTransform;
        private readonly int _poolSize;
        private readonly int _maxChunks;
        private readonly float _bufferTimeSeconds;
        
        private float _lastBufferCheck;
        private const float BUFFER_CHECK_INTERVAL = 1f;

        private Queue<(int2 position, int yLevel, Action<GameObject> callback)> _pendingChunkSetups = new();

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
            _inactiveChunks = new Dictionary<(int2 position, int yLevel), (float timestamp, GameObject gameObject)>();
            _activeChunks = new Dictionary<(int2 position, int yLevel), GameObject>();
            
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
            
            // Create shadow object immediately
            var shadowObject = new GameObject("Shadow");
            shadowObject.transform.parent = chunk.transform;
            shadowObject.transform.localPosition = Vector3.zero;
            
            return chunk;
        }

        public (GameObject chunk, MeshFilter meshFilter, MeshFilter shadowFilter)? GetChunk(int2 position, int yLevel)
        {
            var key = (position, yLevel);
            GameObject chunkObject;
            
            // Check active chunks first
            if (_activeChunks.TryGetValue(key, out chunkObject))
            {
                return (chunkObject, 
                    chunkObject.GetComponent<MeshFilter>(),
                    chunkObject.transform.GetChild(0).GetComponent<MeshFilter>());
            }
            
            // Check inactive chunks
            if (_inactiveChunks.TryGetValue(key, out var inactiveData))
            {
                chunkObject = inactiveData.gameObject;
                _inactiveChunks.Remove(key);
                _activeChunks[key] = chunkObject;
                // Don't activate yet - let the mesh builder handle it
                return (chunkObject,
                    chunkObject.GetComponent<MeshFilter>(),
                    chunkObject.transform.GetChild(0).GetComponent<MeshFilter>());
            }

            // Get or create new chunk
            if (_pool.Count > 0)
            {
                chunkObject = _pool.Dequeue();
            }
            else if (TotalChunksInUse >= _maxChunks)
            {
                ForceReclaimOldestChunks();
                if (_pool.Count == 0) return null;
                chunkObject = _pool.Dequeue();
            }
            else
            {
                chunkObject = CreateChunkGameObject();
            }

            // Setup chunk but don't activate yet
            SetupChunkObject(chunkObject, position, yLevel);
            _activeChunks[key] = chunkObject;
            chunkObject.SetActive(false); // Keep it hidden until mesh is ready

            return (chunkObject,
                chunkObject.GetComponent<MeshFilter>(),
                chunkObject.transform.GetChild(0).GetComponent<MeshFilter>());
        }

        private void SetupChunkObject(GameObject chunk, int2 position, int yLevel)
        {
            chunk.name = $"Chunk_{position.x}_{position.y}_Y{yLevel}";
            chunk.transform.position = new Vector3(
                position.x * ChunkData.SIZE, 
                0, 
                position.y * ChunkData.SIZE
            );
            
            // Ensure components exist
            if (!chunk.TryGetComponent<MeshFilter>(out _))
                chunk.AddComponent<MeshFilter>();
            if (!chunk.TryGetComponent<MeshRenderer>(out var renderer))
                renderer = chunk.AddComponent<MeshRenderer>();
            renderer.material = _chunkMaterial;

            // Setup shadow object
            var shadowObject = chunk.transform.GetChild(0);
            if (!shadowObject.TryGetComponent<MeshFilter>(out _))
                shadowObject.gameObject.AddComponent<MeshFilter>();
            if (!shadowObject.TryGetComponent<MeshRenderer>(out var shadowRenderer))
                shadowRenderer = shadowObject.gameObject.AddComponent<MeshRenderer>();
            
            shadowRenderer.material = _chunkMaterial;
            shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            shadowRenderer.receiveShadows = false;
        }

        private void ForceReclaimOldestChunks()
        {
            var oldestChunks = _inactiveChunks
                .OrderBy(x => x.Value.timestamp)
                .Take(_maxChunks / 4)
                .ToList();

            foreach (var old in oldestChunks)
            {
                var chunk = old.Value.gameObject;
                chunk.SetActive(false);
                _pool.Enqueue(chunk);
                _inactiveChunks.Remove(old.Key);
            }
        }

        public void DeactivateChunk(int2 position, int yLevel)
        {
            var key = (position, yLevel);
            if (_activeChunks.TryGetValue(key, out var chunk))
            {
                chunk.SetActive(false);
                _inactiveChunks[key] = (Time.time, chunk);
                _activeChunks.Remove(key);
            }
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
                var chunk = expired.Value.gameObject;
                chunk.SetActive(false);
                _pool.Enqueue(chunk);
                _inactiveChunks.Remove(expired.Key);
            }
        }

        public void Cleanup()
        {
            foreach (var chunk in _activeChunks.Values)
            {
                if (chunk != null)
                    UnityEngine.Object.Destroy(chunk);
            }
            
            foreach (var inactive in _inactiveChunks.Values)
            {
                if (inactive.gameObject != null)
                    UnityEngine.Object.Destroy(inactive.gameObject);
            }
            
            while (_pool.Count > 0)
            {
                var chunk = _pool.Dequeue();
                if (chunk != null)
                    UnityEngine.Object.Destroy(chunk);
            }
            
            _activeChunks.Clear();
            _inactiveChunks.Clear();
        }

        public bool HasChunkAtPosition(int2 position, int yLevel)
        {
            return _activeChunks.ContainsKey((position, yLevel)) || _inactiveChunks.ContainsKey((position, yLevel));
        }

        public IEnumerable<(int2 position, int yLevel)> GetActiveChunkPositions()
        {
            return _activeChunks.Select(chunk => chunk.Key);
        }

        public bool HasActiveChunkAtPosition(int2 position, int yLevel)
        {
            return _activeChunks.ContainsKey((position, yLevel));
        }

        public void QueueChunkSetup(int2 position, int yLevel, Action<GameObject> callback)
        {
            _pendingChunkSetups.Enqueue((position, yLevel, callback));
        }

        public void ProcessPendingSetups(int maxPerFrame = 8)
        {
            int processed = 0;
            while (_pendingChunkSetups.Count > 0 && processed < maxPerFrame)
            {
                var (pos, yLevel, callback) = _pendingChunkSetups.Dequeue();
                
                // Use GetChunk instead of CreateAndSetupChunk
                var chunkResult = GetChunk(pos, yLevel);
                if (chunkResult.HasValue)
                {
                    var (chunk, _, _) = chunkResult.Value;
                    callback(chunk);
                }
                
                processed++;
            }
        }
    }
} 