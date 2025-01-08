using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;
using WorldSystem.Data;
using WorldSystem.Jobs;
using System.Linq; 

namespace WorldSystem.Generation
{
    public class ChunkGenerator : IChunkGenerator
    {
        public int seed { get; private set; }

        public ChunkGenerator()
        {
            seed = UnityEngine.Random.Range(0, 99999); // Or however you want to set the seed
        }

        // Track which chunks are currently being generated
        private HashSet<int2> _chunksInProgress = new();
        private Dictionary<int2, PendingChunk> _pendingChunks = new();
        private JobHandle _lastJobHandle;

        public event System.Action<ChunkData> OnChunkGenerated;

        // Input: 2D position (x,z) where we want to generate a chunk
        public void QueueChunkGeneration(int2 position)
        {
            if (_chunksInProgress.Contains(position))
                return;

            _chunksInProgress.Add(position);
            StartChunkGeneration(new int3(position.x, 0, position.y)); // Start at y=0
        }

        private void StartChunkGeneration(int3 position)
        {
            _lastJobHandle.Complete();

            var blocks = new NativeArray<byte>(
                ChunkData.SIZE * ChunkData.HEIGHT * ChunkData.SIZE, 
                Allocator.TempJob,
                NativeArrayOptions.ClearMemory
            );

            var genJob = new ChunkGenerationJob
            {
                position = position,
                seed = this.seed,
                blocks = blocks,
                isFullyOpaque = true
            };

            var jobHandle = genJob.Schedule(ChunkData.SIZE * ChunkData.SIZE, 64);
            _lastJobHandle = jobHandle;

            _pendingChunks[new int2(position.x, position.z)] = new PendingChunk
            {
                position = position,
                jobHandle = jobHandle,
                blocks = blocks,
                isValid = true
            };
        }

        // Output: Generated ChunkData via OnChunkGenerated event
        public void Update()
        {
            foreach (var pos in _chunksInProgress.ToList())
            {
                if (!_pendingChunks.TryGetValue(pos, out var chunk) || !chunk.isValid) continue;
                if (!chunk.jobHandle.IsCompleted) continue;

                chunk.jobHandle.Complete();

                var chunkData = new ChunkData
                {
                    position = chunk.position,
                    blocks = chunk.blocks,
                    isEdited = false
                };

                OnChunkGenerated?.Invoke(chunkData);
                _chunksInProgress.Remove(pos);
                
                // Mark as invalid instead of removing immediately
                var invalidChunk = chunk;
                invalidChunk.isValid = false;
                _pendingChunks[pos] = invalidChunk;
            }

            // Cleanup completed chunks
            foreach (var kvp in _pendingChunks.ToList())
            {
                if (!kvp.Value.isValid)
                {
                    if (kvp.Value.blocks.IsCreated)
                        kvp.Value.blocks.Dispose();
                    _pendingChunks.Remove(kvp.Key);
                }
            }
        }

        public bool IsGenerating(int2 position) => _chunksInProgress.Contains(position);

        public void Dispose()
        {
            _lastJobHandle.Complete();
            
            // Ensure all pending chunks are properly disposed
            foreach (var chunk in _pendingChunks.Values)
            {
                if (chunk.isValid && chunk.blocks.IsCreated)
                {
                    chunk.jobHandle.Complete();
                    chunk.blocks.Dispose();
                }
            }
            
            _pendingChunks.Clear();
            _chunksInProgress.Clear();
        }

        private struct PendingChunk
        {
            public int3 position;
            public JobHandle jobHandle;
            public NativeArray<byte> blocks;
            public bool isValid;
        }
    }
}