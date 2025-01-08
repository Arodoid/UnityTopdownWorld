using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;
using WorldSystem.Data;
using WorldSystem.Jobs;
using System.Linq; 
using WorldSystem.Generation.Jobs;

namespace WorldSystem.Generation
{
    public class ChunkGenerator : IChunkGenerator
    {
        public int seed { get; private set; }

        public ChunkGenerator()
        {
            seed = 123; // Or however you want to set the seed
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

            int size = ChunkData.SIZE * ChunkData.SIZE;
            
            // Allocate arrays for all generation steps
            var blocks = new NativeArray<byte>(size * ChunkData.HEIGHT, Allocator.TempJob);
            var continentalness = new NativeArray<float>(size, Allocator.TempJob);
            var biomeData = new NativeArray<BiomeData>(size, Allocator.TempJob);

            // 1. Generate continents
            var continentJob = new ContinentGenerationJob
            {
                chunkPosition = new int2(position.x, position.z),
                seed = this.seed,
                continentalness = continentalness
            };
            var continentHandle = continentJob.Schedule(size, 64);

            // 2. Generate biome regions
            var biomeJob = new BiomeRegionJob
            {
                chunkPosition = new int2(position.x, position.z),
                seed = this.seed,
                continentalness = continentalness,
                biomeData = biomeData
            };
            var biomeHandle = biomeJob.Schedule(size, 64, continentHandle);

            // 3. Generate terrain
            var terrainJob = new TerrainGenerationJob
            {
                position = position,
                seed = this.seed,
                biomeData = biomeData,
                blocks = blocks
            };
            var terrainHandle = terrainJob.Schedule(size, 64, biomeHandle);

            _lastJobHandle = terrainHandle;

            _pendingChunks[new int2(position.x, position.z)] = new PendingChunk
            {
                position = position,
                jobHandle = terrainHandle,
                blocks = blocks,
                continentalness = continentalness,
                biomeData = biomeData,
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
                
                // Mark as invalid and clean up additional arrays
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
                    if (kvp.Value.continentalness.IsCreated)
                        kvp.Value.continentalness.Dispose();
                    if (kvp.Value.biomeData.IsCreated)
                        kvp.Value.biomeData.Dispose();
                    _pendingChunks.Remove(kvp.Key);
                }
            }
        }

        public bool IsGenerating(int2 position) => _chunksInProgress.Contains(position);

        public void Dispose()
        {
            _lastJobHandle.Complete();
            
            foreach (var chunk in _pendingChunks.Values)
            {
                if (chunk.isValid)
                {
                    chunk.jobHandle.Complete();
                    if (chunk.blocks.IsCreated)
                        chunk.blocks.Dispose();
                    if (chunk.continentalness.IsCreated)
                        chunk.continentalness.Dispose();
                    if (chunk.biomeData.IsCreated)
                        chunk.biomeData.Dispose();
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
            public NativeArray<float> continentalness;
            public NativeArray<BiomeData> biomeData;
            public bool isValid;
        }
    }
}