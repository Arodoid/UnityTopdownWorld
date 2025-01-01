using UnityEngine;
using VoxelGame.WorldSystem.Generation.Core;
using VoxelGame.WorldSystem.Generation.Biomes;

namespace VoxelGame.WorldSystem.Generation.Features
{
    /// <summary>
    /// Settings specific to terrain generation
    /// </summary>
    public class TerrainSettings : FeatureSettings
    {
        // Base terrain
        public float baseHeight = 64f;          // The flat terrain level (like Minecraft's sea level)
        public float baseVariation = 3f;        // Subtle variations in the base terrain (small bumps)
        
        // Hills
        public bool generateHills = true;       // Whether this biome has hills
        public float hillsFrequency = 0.02f;    // How often hills appear
        public float hillsHeight = 12f;         // How tall hills are
        public float hillsScale = 0.8f;         // How spread out hills are
        
        // Mountains (could be moved to separate feature if needed)
        public bool generateMountains = false;  // Whether this biome has mountains
        public float mountainFrequency = 0.01f; // How often mountains appear
        public float mountainHeight = 32f;      // How tall mountains are
        public float mountainScale = 1.2f;      // How spread out mountains are
        
        // Block types
        public Block surfaceBlock;       // Top layer (was grass)
        public Block subsurfaceBlock;    // Middle layer (was dirt)
        public Block deepBlock;          // Bottom layer (was stone)
        
        // Layer depths
        public int surfaceDepth = 1;
        public int subsurfaceDepth = 4;
    }

    /// <summary>
    /// Handles base terrain generation.
    /// This is the foundation that other features will modify.
    /// </summary>
    public class TerrainFeature : WorldFeature
    {
        private readonly TerrainSettings terrainSettings;
        private const int BASE_NOISE_INDEX = 0;
        private const int HILLS_NOISE_INDEX = 1;
        private const int MOUNTAIN_NOISE_INDEX = 2;

        public TerrainFeature(TerrainSettings settings) : base(settings)
        {
            this.terrainSettings = settings;
        }

        public override void Apply(Chunk chunk, NoiseGenerator noise)
        {
            // Generate heightmap for this chunk
            float[,] heightMap = GenerateHeightMap(chunk.Position, noise);

            // Apply heightmap to chunk
            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                float height = heightMap[x, z];
                GenerateTerrainColumn(chunk, x, z, height);
            }
        }

        private float[,] GenerateHeightMap(Vector3Int chunkPos, NoiseGenerator noise)
        {
            float[,] heightMap = new float[Chunk.ChunkSize, Chunk.ChunkSize];

            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                float worldX = chunkPos.x * Chunk.ChunkSize + x;
                float worldZ = chunkPos.z * Chunk.ChunkSize + z;

                // Get temperature for biome blending
                float temperature = noise.GetNoise(
                    worldX,
                    worldZ,
                    0,
                    GenerationConstants.Noise.Biomes.SCALE
                );

                // Get blended terrain settings
                TerrainSettings blendedSettings = BiomeBlending.GetBlendedSettings<TerrainSettings>(
                    temperature,
                    GenerationConstants.Noise.Biomes.BLEND_RANGE
                );

                // Start with base height
                float height = blendedSettings.baseHeight;

                // Apply base variation if enabled
                if (blendedSettings.baseVariation > 0)
                {
                    float baseTerrainNoise = noise.GetOctaveNoise(
                        worldX,
                        worldZ,
                        BASE_NOISE_INDEX,
                        blendedSettings.scale,  // Use biome's scale instead of hardcoded value
                        1,  // Single octave for simpler control
                        1f  // Full persistence
                    );
                    height += baseTerrainNoise * blendedSettings.baseVariation;
                }

                // Apply hills if enabled
                if (blendedSettings.generateHills && blendedSettings.hillsHeight > 0)
                {
                    float hillNoise = noise.GetOctaveNoise(
                        worldX,
                        worldZ,
                        HILLS_NOISE_INDEX,
                        blendedSettings.hillsFrequency,
                        1,  // Single octave
                        1f  // Full persistence
                    );
                    height += hillNoise * blendedSettings.hillsHeight * blendedSettings.hillsScale;
                }

                // Apply mountains if enabled
                if (blendedSettings.generateMountains && blendedSettings.mountainHeight > 0)
                {
                    float mountainNoise = noise.GetRidgedNoise(
                        worldX,
                        worldZ,
                        MOUNTAIN_NOISE_INDEX,
                        blendedSettings.mountainFrequency
                    );
                    height += mountainNoise * blendedSettings.mountainHeight * blendedSettings.mountainScale;
                }

                heightMap[x, z] = height;
            }

            return heightMap;
        }

        private void GenerateTerrainColumn(Chunk chunk, int x, int z, float height)
        {
            int localY = Mathf.FloorToInt(height) - (chunk.Position.y * Chunk.ChunkSize);
            
            // If height is below this chunk, fill with solid blocks
            if (localY >= Chunk.ChunkSize)
            {
                for (int y = 0; y < Chunk.ChunkSize; y++)
                {
                    Block blockType = DetermineBlockType(y, localY);
                    chunk.SetBlock(x, y, z, blockType);
                }
            }
            // If height is within this chunk, fill up to height
            else if (localY >= 0)
            {
                for (int y = 0; y <= localY; y++)
                {
                    Block blockType = DetermineBlockType(y, localY);
                    chunk.SetBlock(x, y, z, blockType);
                }
                // Fill rest with air
                for (int y = localY + 1; y < Chunk.ChunkSize; y++)
                {
                    chunk.SetBlock(x, y, z, null);
                }
            }
            // If height is above this chunk, fill with air
            else
            {
                for (int y = 0; y < Chunk.ChunkSize; y++)
                {
                    chunk.SetBlock(x, y, z, null);
                }
            }
        }

        private Block DetermineBlockType(int currentHeight, int surfaceHeight)
        {
            int depthFromSurface = surfaceHeight - currentHeight;

            if (depthFromSurface == 0)
                return terrainSettings.surfaceBlock;
            if (depthFromSurface <= terrainSettings.surfaceDepth)
                return terrainSettings.subsurfaceBlock;
            if (depthFromSurface <= terrainSettings.surfaceDepth + terrainSettings.subsurfaceDepth)
                return terrainSettings.subsurfaceBlock;
            
            return terrainSettings.deepBlock;
        }
    }
} 