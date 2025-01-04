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
        private readonly TerrainSettings defaultSettings;
        private const int BASE_NOISE_INDEX = 0;
        private const int HILLS_NOISE_INDEX = 1;
        private const int MOUNTAIN_NOISE_INDEX = 2;

        public TerrainFeature(TerrainSettings settings) : base(settings)
        {
            this.defaultSettings = settings;
        }

        public override void Apply(Chunk chunk, NoiseGenerator noise)
        {
            var heightMap = new float[Chunk.ChunkSize, Chunk.ChunkSize];
            var blockTypeMap = new BlockColumn[Chunk.ChunkSize, Chunk.ChunkSize];

            // First pass: Calculate heights and block types
            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                float worldX = chunk.Position.x * Chunk.ChunkSize + x;
                float worldZ = chunk.Position.z * Chunk.ChunkSize + z;

                // Get temperature and settings once per column
                float temperature = noise.GetNoise(worldX, worldZ, 0, GenerationConstants.Noise.Biomes.SCALE);
                var settings = BiomeBlending.GetBlendedSettings<TerrainSettings>(temperature);

                // Calculate height
                float height = CalculateColumnHeight(worldX, worldZ, settings, noise);
                heightMap[x, z] = height;

                // Store just the block types instead of entire settings
                blockTypeMap[x, z] = new BlockColumn(
                    settings.surfaceBlock,
                    settings.subsurfaceBlock,
                    settings.deepBlock,
                    settings.surfaceDepth,
                    settings.subsurfaceDepth
                );
            }

            // Second pass: Generate terrain using cached data
            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                GenerateTerrainColumn(chunk, x, z, heightMap[x, z], blockTypeMap[x, z]);
            }
        }

        private float CalculateColumnHeight(float worldX, float worldZ, TerrainSettings settings, NoiseGenerator noise)
        {
            float height = settings.baseHeight;

            if (settings.baseVariation > 0)
            {
                height += noise.GetOctaveNoise(worldX, worldZ, BASE_NOISE_INDEX, settings.scale, 1, 1f) 
                         * settings.baseVariation;
            }

            if (settings.generateHills)
            {
                height += noise.GetOctaveNoise(worldX, worldZ, HILLS_NOISE_INDEX, settings.hillsFrequency, 1, 1f) 
                         * settings.hillsHeight * settings.hillsScale;
            }

            if (settings.generateMountains)
            {
                height += noise.GetRidgedNoise(worldX, worldZ, MOUNTAIN_NOISE_INDEX, settings.mountainFrequency) 
                         * settings.mountainHeight * settings.mountainScale;
            }

            return height;
        }

        private readonly struct BlockColumn
        {
            public readonly Block Surface;
            public readonly Block Subsurface;
            public readonly Block Deep;
            public readonly int SurfaceDepth;
            public readonly int SubsurfaceDepth;

            public BlockColumn(Block surface, Block subsurface, Block deep, int surfaceDepth, int subsurfaceDepth)
            {
                Surface = surface;
                Subsurface = subsurface;
                Deep = deep;
                SurfaceDepth = surfaceDepth;
                SubsurfaceDepth = subsurfaceDepth;
            }
        }

        private void GenerateTerrainColumn(Chunk chunk, int x, int z, float height, BlockColumn blocks)
        {
            int localY = Mathf.FloorToInt(height) - (chunk.Position.y * Chunk.ChunkSize);
            
            if (localY >= Chunk.ChunkSize)
            {
                for (int y = 0; y < Chunk.ChunkSize; y++)
                {
                    chunk.SetBlock(x, y, z, DetermineBlockType(y, localY, blocks));
                }
            }
            else if (localY >= 0)
            {
                for (int y = 0; y <= localY; y++)
                {
                    chunk.SetBlock(x, y, z, DetermineBlockType(y, localY, blocks));
                }
                for (int y = localY + 1; y < Chunk.ChunkSize; y++)
                {
                    chunk.SetBlock(x, y, z, null);
                }
            }
            else
            {
                for (int y = 0; y < Chunk.ChunkSize; y++)
                {
                    chunk.SetBlock(x, y, z, null);
                }
            }
        }

        private Block DetermineBlockType(int currentHeight, int surfaceHeight, BlockColumn blocks)
        {
            int depthFromSurface = surfaceHeight - currentHeight;

            if (depthFromSurface == 0)
                return blocks.Surface;
            if (depthFromSurface <= blocks.SurfaceDepth)
                return blocks.Subsurface;
            if (depthFromSurface <= blocks.SurfaceDepth + blocks.SubsurfaceDepth)
                return blocks.Subsurface;
            
            return blocks.Deep;
        }

        private TerrainSettings BlendSettings(TerrainSettings a, TerrainSettings b, float blend)
        {
            return new TerrainSettings
            {
                enabled = blend < 0.5f ? a.enabled : b.enabled,
                scale = Mathf.Lerp(a.scale, b.scale, blend),
                strength = Mathf.Lerp(a.strength, b.strength, blend),
                baseHeight = Mathf.Lerp(a.baseHeight, b.baseHeight, blend),
                baseVariation = Mathf.Lerp(a.baseVariation, b.baseVariation, blend),
                generateHills = blend < 0.5f ? a.generateHills : b.generateHills,
                hillsFrequency = Mathf.Lerp(a.hillsFrequency, b.hillsFrequency, blend),
                hillsHeight = Mathf.Lerp(a.hillsHeight, b.hillsHeight, blend),
                hillsScale = Mathf.Lerp(a.hillsScale, b.hillsScale, blend),
                generateMountains = blend < 0.5f ? a.generateMountains : b.generateMountains,
                mountainFrequency = Mathf.Lerp(a.mountainFrequency, b.mountainFrequency, blend),
                mountainHeight = Mathf.Lerp(a.mountainHeight, b.mountainHeight, blend),
                mountainScale = Mathf.Lerp(a.mountainScale, b.mountainScale, blend),
                surfaceBlock = blend < 0.5f ? a.surfaceBlock : b.surfaceBlock,
                subsurfaceBlock = blend < 0.5f ? a.subsurfaceBlock : b.subsurfaceBlock,
                deepBlock = blend < 0.5f ? a.deepBlock : b.deepBlock,
                surfaceDepth = Mathf.RoundToInt(Mathf.Lerp(a.surfaceDepth, b.surfaceDepth, blend)),
                subsurfaceDepth = Mathf.RoundToInt(Mathf.Lerp(a.subsurfaceDepth, b.subsurfaceDepth, blend))
            };
        }
    }
} 