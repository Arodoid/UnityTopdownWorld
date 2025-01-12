using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using WorldSystem.Data;
using System;
using Unity.Collections;

namespace WorldSystem.Persistence
{
    public class ChunkPersistenceManager
    {
        private readonly string _worldSavePath;
        private readonly Dictionary<int2, SerializableChunkData> _modifiedChunks;
        private readonly object _lockObject = new object();
        private readonly Queue<int2> _saveQueue;
        private bool _isProcessingSaves;

        public ChunkPersistenceManager(string worldName)
        {
            _worldSavePath = Path.Combine(Application.persistentDataPath, "Worlds", worldName, "chunks");
            _modifiedChunks = new Dictionary<int2, SerializableChunkData>();
            _saveQueue = new Queue<int2>();
            Directory.CreateDirectory(_worldSavePath);
        }

        public void MarkBlockModified(int2 chunkPos, int blockIndex, byte newBlockType)
        {
            lock (_lockObject)
            {
                if (!_modifiedChunks.TryGetValue(chunkPos, out var chunkData))
                {
                    chunkData = new SerializableChunkData(chunkPos);
                    _modifiedChunks[chunkPos] = chunkData;
                }

                chunkData.modifications[blockIndex] = newBlockType;
                chunkData.lastModified = DateTime.Now.Ticks;
                chunkData.isDirty = true;

                // Queue for saving
                if (!_saveQueue.Contains(chunkPos))
                {
                    _saveQueue.Enqueue(chunkPos);
                }

                // Start processing saves if not already running
                if (!_isProcessingSaves)
                {
                    ProcessSaveQueueAsync();
                }
            }
        }

        public bool HasModifications(int2 chunkPos)
        {
            lock (_lockObject)
            {
                return _modifiedChunks.ContainsKey(chunkPos);
            }
        }

        public void ApplyModifications(int2 chunkPos, NativeArray<byte> blocks)
        {
            lock (_lockObject)
            {
                if (_modifiedChunks.TryGetValue(chunkPos, out var chunkData))
                {
                    foreach (var modification in chunkData.modifications)
                    {
                        blocks[modification.Key] = modification.Value;
                    }
                }
            }
        }

        private async void ProcessSaveQueueAsync()
        {
            _isProcessingSaves = true;

            while (_saveQueue.Count > 0)
            {
                int2 chunkPos;
                SerializableChunkData chunkData;

                lock (_lockObject)
                {
                    chunkPos = _saveQueue.Dequeue();
                    if (!_modifiedChunks.TryGetValue(chunkPos, out chunkData))
                        continue;
                }

                await SaveChunkAsync(chunkPos, chunkData);
            }

            _isProcessingSaves = false;
        }

        private async Task SaveChunkAsync(int2 chunkPos, SerializableChunkData chunkData)
        {
            string chunkPath = GetChunkPath(chunkPos);

            try
            {
                await Task.Run(() =>
                {
                    lock (_lockObject)
                    {
                        using (FileStream stream = File.Create(chunkPath))
                        {
                            var formatter = new BinaryFormatter();
                            formatter.Serialize(stream, chunkData);
                        }
                        chunkData.isDirty = false;
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save chunk at {chunkPos}: {e.Message}");
            }
        }

        public async Task<SerializableChunkData> LoadChunkAsync(int2 chunkPos)
        {
            string chunkPath = GetChunkPath(chunkPos);

            if (!File.Exists(chunkPath))
                return null;

            try
            {
                return await Task.Run(() =>
                {
                    using (FileStream stream = File.OpenRead(chunkPath))
                    {
                        var formatter = new BinaryFormatter();
                        var chunkData = (SerializableChunkData)formatter.Deserialize(stream);
                        
                        lock (_lockObject)
                        {
                            _modifiedChunks[chunkPos] = chunkData;
                        }
                        
                        return chunkData;
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load chunk at {chunkPos}: {e.Message}");
                return null;
            }
        }

        private string GetChunkPath(int2 chunkPos)
        {
            return Path.Combine(_worldSavePath, $"chunk_{chunkPos.x}_{chunkPos.y}.dat");
        }

        public void SaveAll()
        {
            lock (_lockObject)
            {
                foreach (var chunk in _modifiedChunks.Values)
                {
                    if (chunk.isDirty)
                    {
                        _saveQueue.Enqueue(chunk.position);
                    }
                }
            }

            // Wait for all saves to complete
            while (_isProcessingSaves)
            {
                Task.Delay(100).Wait();
            }
        }

        public async Task SaveNewChunkAsync(int2 position, ChunkData chunk)
        {
            var serializedChunk = new SerializableChunkData(position)
            {
                blocks = chunk.blocks.ToArray()
            };
            
            await SaveChunkAsync(position, serializedChunk);
        }

        public async Task SaveModifiedChunkAsync(int2 position, Dictionary<int, byte> modifications)
        {
            var serializedChunk = new SerializableChunkData(position)
            {
                modifications = modifications
            };
            
            await SaveChunkAsync(position, serializedChunk);
        }
    }
} 