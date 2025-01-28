using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;

namespace WorldSystem.Generation
{
    public class NoiseCache
    {
        private readonly Dictionary<int2, NativeArray<float>> _heightCache = new();
        private readonly Dictionary<int2, NativeArray<float>> _temperatureCache = new();
        private readonly Dictionary<int2, NativeArray<float>> _humidityCache = new();
        private readonly Dictionary<int3, NativeArray<float>> _3dNoiseCache = new();
        
        private readonly FastNoise _3dNoise;
        private readonly FastNoise _heightNoise;
        private readonly FastNoise _temperatureNoise;
        private readonly FastNoise _humidityNoise;
        private readonly int _seed;

        public NoiseCache(int seed)
        {
            _seed = seed;
            _3dNoise = FastNoise.FromEncodedNodeTree("BwA=");
            _heightNoise = FastNoise.FromEncodedNodeTree("HgATAB+Faz8bAB0AHgAeACEAIgBxPQpBmpkZPxkAHwAgABMACtcjPxkAFwAAAAAAAACAPwrXo78AAABAGgAAAACAPwEkAAIAAAAPAAEAAAAAAABADQAIAAAAAAAAQAcAAAAAAD8AAAAAAAAAAAA/AAAAAAABDQACAAAArkcBQBoAARMAPQrXPv//BQAAKVwvQADXo3A/AIXr0cAA16OwPwBmZrZBAJqZmT4ASOGaQAEXAAAAgL8AAIA/7FE4vs3MzD0bABMAMzPzPxYAAQAAAB8AIAAXAArXoz2PwvU9AAAAAAAAgD///wQAAM3MTD4ArkeRQAAK1yO+AHsUrkAA16NwP///FAABDQADAAAAexSuPhsACAAAcT2KPwDsUTg/AFyPskABGQAbAP//GQAA16PQQAAfhWu/ASEADwAEAAAAKVwPQAsAAQAAAAAAAAAAAAAAAwAAAABmZqY/ABSuRz8AcT0KQBcApHC9PwAAwD8pXA8+CtejPiUAKVyPvnsULj8K1+NASOH6PwUAAQAAAAAAAAAK1yM9AAAAAAAAAAAAAAAAPwD2KFw/AKRwPT8AzczMvQ==");
            _temperatureNoise = FastNoise.FromEncodedNodeTree("BwA=");
            _humidityNoise = FastNoise.FromEncodedNodeTree("BwA=");
        }

        public NativeArray<float> Get3DNoise(int3 chunkPos)
        {
            if (_3dNoiseCache.TryGetValue(chunkPos, out var cached))
                return cached;

            var noise = Generate3DNoise(chunkPos);
            _3dNoiseCache[chunkPos] = noise;
            return noise;
        }

        public NativeArray<float> GetHeightNoise(int2 chunkPos)
        {
            if (_heightCache.TryGetValue(chunkPos, out var cached))
                return cached;

            var noise = Generate2DNoise(_heightNoise, chunkPos, 0.004f, _seed);
            _heightCache[chunkPos] = noise;
            return noise;
        }

        public NativeArray<float> GetTemperatureNoise(int2 chunkPos)
        {
            if (_temperatureCache.TryGetValue(chunkPos, out var cached))
                return cached;

            var noise = Generate2DNoise(_temperatureNoise, chunkPos, 0.004f, _seed + 1);
            _temperatureCache[chunkPos] = noise;
            return noise;
        }

        public NativeArray<float> GetHumidityNoise(int2 chunkPos)
        {
            if (_humidityCache.TryGetValue(chunkPos, out var cached))
                return cached;

            var noise = Generate2DNoise(_humidityNoise, chunkPos, 0.004f, _seed + 2);
            _humidityCache[chunkPos] = noise;
            return noise;
        }

        private NativeArray<float> Generate3DNoise(int3 chunkPos)
        {
            var noise = new NativeArray<float>(
                Data.ChunkData.SIZE * Data.ChunkData.SIZE * Data.ChunkData.HEIGHT,
                Allocator.Persistent);

            float scale3D = 0.03f;
            
            for (int y = 0; y < Data.ChunkData.HEIGHT; y++)
            {
                for (int x = 0; x < Data.ChunkData.SIZE; x++)
                {
                    for (int z = 0; z < Data.ChunkData.SIZE; z++)
                    {
                        float worldX = chunkPos.x * Data.ChunkData.SIZE + x;
                        float worldY = y;
                        float worldZ = chunkPos.z * Data.ChunkData.SIZE + z;

                        int index = x + (z * Data.ChunkData.SIZE) + (y * Data.ChunkData.SIZE * Data.ChunkData.SIZE);
                        float value = _3dNoise.GenSingle3D(
                            worldX * scale3D, 
                            worldY * scale3D, 
                            worldZ * scale3D, 
                            _seed);
                        noise[index] = (value + 1f) * 0.5f;
                    }
                }
            }
            
            return noise;
        }

        private NativeArray<float> Generate2DNoise(FastNoise noise, int2 chunkPos, float scale, int seed)
        {
            var values = new NativeArray<float>(
                Data.ChunkData.SIZE * Data.ChunkData.SIZE,
                Allocator.Persistent);

            for (int x = 0; x < Data.ChunkData.SIZE; x++)
            {
                for (int z = 0; z < Data.ChunkData.SIZE; z++)
                {
                    float worldX = chunkPos.x * Data.ChunkData.SIZE + x;
                    float worldZ = chunkPos.y * Data.ChunkData.SIZE + z;
                    
                    int index = x + z * Data.ChunkData.SIZE;
                    float value = noise.GenSingle2D(worldX * scale, worldZ * scale, seed);
                    values[index] = (value + 1f) * 0.5f;
                }
            }

            return values;
        }

        public void Dispose()
        {
            foreach (var array in _heightCache.Values)
                array.Dispose();
            foreach (var array in _temperatureCache.Values)
                array.Dispose();
            foreach (var array in _humidityCache.Values)
                array.Dispose();
            foreach (var array in _3dNoiseCache.Values)
                array.Dispose();
            
            _heightCache.Clear();
            _temperatureCache.Clear();
            _humidityCache.Clear();
            _3dNoiseCache.Clear();
        }
    }
} 