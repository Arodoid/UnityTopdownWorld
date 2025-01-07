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
            var chunk = new GameObject("Chunk Pool Object");
            chunk.transform.parent = _parentTransform;
            
            // Add components for render mesh
            chunk.AddComponent<MeshFilter>();
            chunk.AddComponent<MeshRenderer>().material = _chunkMaterial;
            
            // Add components for shadow mesh
            var shadowObject = new GameObject("Shadow Mesh");
            shadowObject.transform.parent = chunk.transform;
            shadowObject.transform.localPosition = Vector3.zero;
            var shadowMeshFilter = shadowObject.AddComponent<MeshFilter>();
            var shadowMeshRenderer = shadowObject.AddComponent<MeshRenderer>();
            shadowMeshRenderer.material = _chunkMaterial;
            shadowMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            shadowMeshRenderer.receiveShadows = false;
            
            return chunk;
        }

        public (GameObject chunk, MeshFilter meshFilter, MeshFilter shadowFilter)? GetChunk(int2 position)
        {
            GameObject chunkObject;
            
            // Try to reclaim chunks if at limit
            if (_pool.Count == 0 && TotalChunksInUse >= _maxChunks)
            {
                ForceReclaimOldestChunks();
            }

            // Try to get a chunk
            if (_inactiveChunks.TryGetValue(position, out var inactive))
            {
                chunkObject = inactive.gameObject;
                _inactiveChunks.Remove(position);
                chunkObject.SetActive(true);
            }
            else if (_pool.Count > 0)
            {
                chunkObject = _pool.Dequeue();
                chunkObject.SetActive(true);
            }
            else
            {
                return null;
            }

            chunkObject.name = $"Chunk {position.x},{position.y}";
            chunkObject.transform.position = new Vector3(position.x * ChunkData.SIZE, 0, position.y * ChunkData.SIZE);

            var meshFilter = chunkObject.GetComponent<MeshFilter>();
            var shadowMeshFilter = meshFilter.gameObject.transform.GetChild(0).GetComponent<MeshFilter>();
            
            _activeChunks[position] = chunkObject;
            
            return (chunkObject, meshFilter, shadowMeshFilter);
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

        public void DeactivateChunk(int2 position)
        {
            if (_activeChunks.TryGetValue(position, out var chunk))
            {
                _inactiveChunks[position] = (Time.time, chunk);
                chunk.SetActive(false);
                _activeChunks.Remove(position);
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

        public bool HasChunkAtPosition(int2 position)
        {
            return _activeChunks.ContainsKey(position) || _inactiveChunks.ContainsKey(position);
        }

        public IEnumerable<int2> GetActiveChunkPositions()
        {
            return _activeChunks.Keys;
        }

        public bool HasActiveChunkAtPosition(int2 position)
        {
            return _activeChunks.ContainsKey(position);
        }
    }
} 