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
        private HashSet<int2> _columnsBeingProcessed = new();
        private Dictionary<int2, (NativeArray<HeightPoint> heightMap, List<PendingChunk> chunks)> _pendingColumns = new();
        private NativeArray<JobHandle> _batchHandles;
        private const int BATCH_SIZE = 256;

        public event System.Action<ChunkData> OnChunkGenerated;

        public ChunkGenerator()
        {
            _batchHandles = new NativeArray<JobHandle>(BATCH_SIZE, Allocator.Persistent);
        }

        public void QueueChunkGeneration(int2 position)
        {
            if (_columnsBeingProcessed.Contains(position))
                return;

            _columnsBeingProcessed.Add(position);
            
            // Create one heightmap for the entire column
            var heightMap = new NativeArray<HeightPoint>(ChunkData.SIZE * ChunkData.SIZE, Allocator.Persistent);
            _pendingColumns[position] = (heightMap, new List<PendingChunk>());
            
            // Start with top chunk
            StartChunkGeneration(new int3(position.x, 15, position.y));
        }

        private void StartChunkGeneration(int3 position)
        {
            var blocks = new NativeArray<byte>(ChunkData.SIZE * ChunkData.SIZE * ChunkData.SIZE, Allocator.Persistent);
            var int2Pos = new int2(position.x, position.z);
            
            var genJob = new ChunkGenerationJob
            {
                position = position,
                seed = 123,
                blocks = blocks,
                heightMap = _pendingColumns[int2Pos].heightMap,  // Use the column's heightMap
                isFullyOpaque = true
            };

            var jobHandle = genJob.Schedule(ChunkData.SIZE * ChunkData.SIZE, 64);
            
            _pendingColumns[int2Pos].chunks.Add(new PendingChunk
            {
                position = position,
                jobHandle = jobHandle,
                blocks = blocks,
                isFullyOpaque = genJob.isFullyOpaque
            });
        }

        public bool IsGenerating(int2 position) => _columnsBeingProcessed.Contains(position);

        public void Update()
        {
            foreach (var columnPos in _columnsBeingProcessed.ToList())
            {
                var (heightMap, chunks) = _pendingColumns[columnPos];
                if (chunks.Count == 0) continue;

                var currentChunk = chunks[chunks.Count - 1];
                if (!currentChunk.jobHandle.IsCompleted) continue;

                currentChunk.jobHandle.Complete();
                chunks.RemoveAt(chunks.Count - 1);

                // If this is not our final chunk
                if (!currentChunk.isFullyOpaque && currentChunk.position.y > 0)
                {
                    var chunkData = new ChunkData
                    {
                        position = currentChunk.position,
                        blocks = currentChunk.blocks,  // ChunkData takes ownership
                        heightMap = heightMap,         // Share the column heightmap
                        isEdited = false
                    };
                    OnChunkGenerated?.Invoke(chunkData);
                    
                    StartChunkGeneration(new int3(
                        currentChunk.position.x,
                        currentChunk.position.y - 1,
                        currentChunk.position.z
                    ));
                }
                else
                {
                    // We're done with this column
                    var chunkData = new ChunkData
                    {
                        position = currentChunk.position,
                        blocks = currentChunk.blocks,  // ChunkData takes ownership
                        heightMap = heightMap,         // Share the column heightmap
                        isEdited = false
                    };
                    OnChunkGenerated?.Invoke(chunkData);
                    
                    _columnsBeingProcessed.Remove(columnPos);
                    
                    // Clean up any remaining chunks in the column
                    foreach (var chunk in chunks)
                    {
                        chunk.jobHandle.Complete();
                        if (chunk.blocks.IsCreated) chunk.blocks.Dispose();
                    }
                    _pendingColumns.Remove(columnPos);
                }
            }
        }

        public void Dispose()
        {
            foreach (var (heightMap, chunks) in _pendingColumns.Values)
            {
                if (heightMap.IsCreated) heightMap.Dispose();
                foreach (var chunk in chunks)
                {
                    chunk.jobHandle.Complete();
                    if (chunk.blocks.IsCreated) chunk.blocks.Dispose();
                }
            }
            _pendingColumns.Clear();
            _columnsBeingProcessed.Clear();

            if (_batchHandles.IsCreated)
                _batchHandles.Dispose();
        }

        private struct PendingChunk
        {
            public int3 position;
            public JobHandle jobHandle;
            public NativeArray<byte> blocks;
            public bool isFullyOpaque;
        }
    }
} 