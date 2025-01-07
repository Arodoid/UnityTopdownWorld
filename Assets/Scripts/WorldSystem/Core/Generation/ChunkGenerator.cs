using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;
using WorldSystem.Data;
using WorldSystem.Jobs;

namespace WorldSystem.Generation
{
    public class ChunkGenerator : IChunkGenerator
    {
        private HashSet<int2> _chunksBeingProcessed = new();
        private List<PendingChunk> _pendingChunks = new();
        private NativeArray<JobHandle> _batchHandles;
        private const int BATCH_SIZE = 256;

        public event System.Action<ChunkData> OnChunkGenerated;

        public ChunkGenerator()
        {
            _batchHandles = new NativeArray<JobHandle>(BATCH_SIZE, Allocator.Persistent);
        }

        public void QueueChunkGeneration(int2 position)
        {
            if (_chunksBeingProcessed.Contains(position))
                return;

            _chunksBeingProcessed.Add(position);
            StartChunkGeneration(position);
        }

        public bool IsGenerating(int2 position) => _chunksBeingProcessed.Contains(position);

        public void Update()
        {
            // Check for completed jobs
            for (int i = _pendingChunks.Count - 1; i >= 0; i--)
            {
                var chunk = _pendingChunks[i];
                if (chunk.jobHandle.IsCompleted)
                {
                    chunk.jobHandle.Complete();
                    
                    var chunkData = new ChunkData
                    {
                        position = chunk.position,
                        blocks = chunk.blocks,
                        heightMap = chunk.heightMap,
                        isEdited = false
                    };

                    OnChunkGenerated?.Invoke(chunkData);
                    
                    // Dispose of the arrays after the event is handled
                    chunk.blocks.Dispose();
                    chunk.heightMap.Dispose();
                    
                    _pendingChunks.RemoveAt(i);
                    _chunksBeingProcessed.Remove(chunk.position);
                }
            }
        }

        private void StartChunkGeneration(int2 position)
        {
            // Create arrays with Persistent allocator
            var blocks = new NativeArray<byte>(ChunkData.SIZE * ChunkData.SIZE * ChunkData.SIZE, Allocator.Persistent);
            var heightMap = new NativeArray<HeightPoint>(ChunkData.SIZE * ChunkData.SIZE, Allocator.Persistent);

            var genJob = new ChunkGenerationJob
            {
                position = position,
                seed = 123,
                blocks = blocks,
                heightMap = heightMap
            };

            var jobHandle = genJob.Schedule(ChunkData.SIZE * ChunkData.SIZE, 64);

            _pendingChunks.Add(new PendingChunk
            {
                position = position,
                jobHandle = jobHandle,
                blocks = blocks,
                heightMap = heightMap
            });
        }

        public void Dispose()
        {
            // Complete and dispose all pending jobs before disposal
            foreach (var chunk in _pendingChunks)
            {
                chunk.jobHandle.Complete();
                if (chunk.blocks.IsCreated) chunk.blocks.Dispose();
                if (chunk.heightMap.IsCreated) chunk.heightMap.Dispose();
            }
            _pendingChunks.Clear();
            _chunksBeingProcessed.Clear();

            if (_batchHandles.IsCreated)
                _batchHandles.Dispose();
        }

        private struct PendingChunk
        {
            public int2 position;
            public JobHandle jobHandle;
            public NativeArray<byte> blocks;
            public NativeArray<HeightPoint> heightMap;
        }
    }
} 