using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using VoxelGame.Utilities;
using VoxelGame.WorldSystem.Generation.Features;
using VoxelGame.WorldSystem.Generation.Biomes;

namespace VoxelGame.WorldSystem.Generation.Core
{
    /// <summary>
    /// Orchestrates the world generation process.
    /// Coordinates between biomes and features but doesn't implement generation logic directly.
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        [SerializeField] private int worldSeed;
        [SerializeField] private BiomeType defaultBiome = BiomeType.Plains;  // Can be set in Unity Inspector
        
        private NoiseGenerator noiseGenerator;
        private readonly List<IWorldFeature> features = new();
        private bool initialized;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;

            if (worldSeed == 0)
                worldSeed = Random.Range(int.MinValue, int.MaxValue);
            
            noiseGenerator = new NoiseGenerator(worldSeed);
            InitializeFeatures();
            
            initialized = true;
        }

        private void InitializeFeatures()
        {
            if (!initialized)
            {
                // Make sure BiomeRegistry is initialized first
                BiomeRegistry.Initialize();
                
                // Use the configured default biome's settings
                var defaultBiomeSettings = BiomeRegistry.GetBiome(defaultBiome).GetFeatureSettings<TerrainSettings>();
                features.Add(new TerrainFeature(defaultBiomeSettings));
                
                // Add other features...
                initialized = true;
            }
        }

        /// <summary>
        /// Generate a new chunk at the given position using the object pool
        /// </summary>
        public async Task<Chunk> GenerateChunkPooled(Vector3Int position, ObjectPool<Chunk> chunkPool)
        {
            if (!initialized) Initialize();

            return await Task.Run(() =>
            {
                Chunk chunk = chunkPool.Get();
                chunk.SetPosition(position);
                GenerateChunkData(chunk);
                return chunk;
            });
        }

        /// <summary>
        /// Generate a new chunk at the given position without pooling
        /// </summary>
        public Task<Chunk> GenerateChunk(Vector3Int position)
        {
            if (!initialized) Initialize();

            return Task.Run(() =>
            {
                var chunk = new Chunk(position);
                GenerateChunkData(chunk);
                return chunk;
            });
        }

        private void GenerateChunkData(Chunk chunk)
        {
            float temperature = GetTemperature(chunk.Position.x, chunk.Position.z);
            
            foreach (var feature in features)
            {
                var settings = BiomeBlending.GetBlendedSettings<FeatureSettings>(temperature);
                if (settings.enabled)
                {
                    feature.Apply(chunk, noiseGenerator);
                }
            }
        }

        /// <summary>
        /// Get temperature value for world position
        /// </summary>
        private float GetTemperature(float x, float z)
        {
            return noiseGenerator.GetNoise(
                x, z, 
                0, 
                GenerationConstants.Noise.Biomes.SCALE
            );
        }

        /// <summary>
        /// Get the biome type at a world position
        /// </summary>
        public BiomeType GetBiomeAt(Vector3Int position)
        {
            float temperature = GetTemperature(position.x, position.z);
            return BiomeRegistry.GetBiomeType(temperature);
        }

        /// <summary>
        /// Get the world seed
        /// </summary>
        public int Seed => worldSeed;
    }
}