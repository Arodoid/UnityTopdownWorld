using UnityEngine;
using VoxelGame.WorldSystem.Generation.Core;
using VoxelGame.WorldSystem.Biomes;

namespace VoxelGame.WorldSystem.Generation.Features
{
    public class TerrainFeatureGenerator
    {
        private readonly NoiseGenerator noiseGenerator;

        // Cave generation settings
        private const float CAVE_SCALE = 0.02f;        // How large the caves are
        private const float CAVE_THRESHOLD = 0.65f;    // Higher = fewer caves
        private const int MIN_CAVE_HEIGHT = 5;         // Minimum Y level for caves
        private const int MAX_CAVE_HEIGHT = 48;        // Maximum Y level for caves
        private const float CAVE_BLEND_RANGE = 8f;     // Smooth cave endings

        // Ravine generation settings
        private const float RAVINE_SCALE = 0.002f;         // Much larger features
        private const float RAVINE_THRESHOLD = 0.8f;       // Higher threshold for fewer, more defined ravines
        private const float RAVINE_WIDTH = 22f;            // Wider ravines
        private const float RAVINE_DEPTH = 64f;            // Same depth
        private const int MIN_RAVINE_HEIGHT = 64;
        private const int MAX_RAVINE_HEIGHT = 80;
        private const float RAVINE_VERTICAL_SCALE = 0.005f;// Smoother height variations
        private const float RAVINE_PATH_SCALE = 0.015f;    // Scale for the ravine path variation

        public TerrainFeatureGenerator(NoiseGenerator noiseGenerator)
        {
            this.noiseGenerator = noiseGenerator;
        }

        public void GenerateCaves(Chunk chunk, BiomeData biomeData)
        {
            int worldY = chunk.Position.y * Chunk.ChunkSize;

            // Skip if chunk is outside cave generation range
            if (worldY > MAX_CAVE_HEIGHT || worldY + Chunk.ChunkSize < MIN_CAVE_HEIGHT)
                return;

            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int y = 0; y < Chunk.ChunkSize; y++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                float worldX = chunk.Position.x * Chunk.ChunkSize + x;
                float worldZ = chunk.Position.z * Chunk.ChunkSize + z;
                int absoluteY = worldY + y;

                // Skip if outside vertical range or at bottom layer
                if (absoluteY < MIN_CAVE_HEIGHT || absoluteY > MAX_CAVE_HEIGHT || absoluteY <= 0)
                    continue;

                // Get cave noise value
                float caveNoise = GetCaveNoise(worldX, absoluteY, worldZ);

                // Apply vertical blending near limits
                float verticalBlendFactor = GetVerticalBlendFactor(absoluteY);
                caveNoise *= verticalBlendFactor;

                // Carve cave if noise is above threshold
                if (caveNoise > CAVE_THRESHOLD)
                {
                    Block currentBlock = chunk.GetBlock(x, y, z);
                    if (currentBlock != null)
                    {
                        chunk.SetBlock(x, y, z, null); // Set to air
                    }
                }
            }
        }

        public void GenerateRavines(Chunk chunk, BiomeData biomeData)
        {
            int worldY = chunk.Position.y * Chunk.ChunkSize;

            if (worldY > MAX_RAVINE_HEIGHT || worldY < MIN_RAVINE_HEIGHT)
                return;

            // Calculate ravine path for this area
            float pathAngle = GetRavinePath(chunk.Position.x * Chunk.ChunkSize, chunk.Position.z * Chunk.ChunkSize);
            Vector2 pathDir = new Vector2(Mathf.Cos(pathAngle), Mathf.Sin(pathAngle));

            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int y = 0; y < Chunk.ChunkSize; y++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                float worldX = chunk.Position.x * Chunk.ChunkSize + x;
                float worldZ = chunk.Position.z * Chunk.ChunkSize + z;
                int absoluteY = worldY + y;

                if (absoluteY <= 0)
                    continue;

                // Calculate distance from ravine center line
                float distanceFromPath = DistanceFromRavinePath(worldX, worldZ, pathDir);
                
                // Get base ravine noise for this location
                float ravineNoise = GetRavineNoise(worldX, worldZ);
                
                if (ravineNoise > RAVINE_THRESHOLD && distanceFromPath < RAVINE_WIDTH)
                {
                    // Calculate ravine center height
                    float heightNoise = noiseGenerator.GetNoise(worldX, worldZ, 2, RAVINE_VERTICAL_SCALE);
                    float ravineHeight = Mathf.Lerp(MIN_RAVINE_HEIGHT, MAX_RAVINE_HEIGHT, heightNoise);
                    
                    // Calculate vertical profile
                    float verticalDistance = Mathf.Abs(absoluteY - ravineHeight);
                    float verticalFactor = 1f - (verticalDistance / (RAVINE_DEPTH * 0.5f));
                    
                    if (verticalFactor > 0)
                    {
                        // Smooth distance falloff
                        float distanceFactor = 1f - (distanceFromPath / RAVINE_WIDTH);
                        distanceFactor = Mathf.SmoothStep(0f, 1f, distanceFactor);
                        
                        // Combine factors
                        float carveStrength = distanceFactor * verticalFactor;
                        
                        // Apply smoother carving
                        if (carveStrength > 0.3f)
                        {
                            chunk.SetBlock(x, y, z, null);
                        }
                    }
                }
            }
        }

        private float GetRavinePath(float x, float z)
        {
            // Get a smooth angle variation for the ravine path
            float baseAngle = noiseGenerator.GetNoise(x, z, 1, RAVINE_PATH_SCALE) * Mathf.PI * 2f;
            float variation = noiseGenerator.GetNoise(x, z, 2, RAVINE_PATH_SCALE * 2f) * 0.5f;
            return baseAngle + variation;
        }

        private float DistanceFromRavinePath(float x, float z, Vector2 pathDir)
        {
            // Project point onto ravine path line
            Vector2 point = new Vector2(x, z);
            Vector2 pathNormal = new Vector2(-pathDir.y, pathDir.x);
            return Mathf.Abs(Vector2.Dot(point, pathNormal));
        }

        private float GetRavineNoise(float x, float z)
        {
            // Simplified noise for ravine presence
            float baseNoise = noiseGenerator.GetRidgedNoise(x, z, 3, RAVINE_SCALE);
            float smoothing = noiseGenerator.GetNoise(x, z, 4, RAVINE_SCALE * 3f);
            return Mathf.Lerp(baseNoise, smoothing, 0.3f); // More weight on the base noise
        }

        private float GetCaveNoise(float x, float y, float z)
        {
            // Use lower offset indices (0-2) for cave generation
            float noise1 = noiseGenerator.GetNoise(x, z, 0, CAVE_SCALE);
            float noise2 = noiseGenerator.GetNoise(x, y, 1, CAVE_SCALE);
            float noise3 = noiseGenerator.GetNoise(y, z, 2, CAVE_SCALE);

            // Combine noise samples
            return (noise1 + noise2 + noise3) / 3f;
        }

        private float GetVerticalBlendFactor(int y)
        {
            // Smooth transition at cave system boundaries
            float bottomBlend = Mathf.SmoothStep(0f, 1f, (y - MIN_CAVE_HEIGHT) / CAVE_BLEND_RANGE);
            float topBlend = Mathf.SmoothStep(1f, 0f, (y - (MAX_CAVE_HEIGHT - CAVE_BLEND_RANGE)) / CAVE_BLEND_RANGE);
            return Mathf.Min(bottomBlend, topBlend);
        }
    }
} 