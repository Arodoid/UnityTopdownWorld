using UnityEngine;
using VoxelGame.Entities;

namespace VoxelGame.WorldSystem.Generation
{
    public class FeatureGenerator
    {
        private readonly float baseTreeDensity = 0.05f;
        private readonly float[] offsets;

        public FeatureGenerator()
        {
            offsets = new float[2];
            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = Random.Range(-10000f, 10000f);
            }
        }

        public void PopulateChunkFeatures(Chunk chunk, Vector3Int chunkPosition, float[,] heightMap, BiomeData biomeData)
        {
            System.Random random = new System.Random(GetChunkSeed(chunkPosition));
            Vector3Int worldPos = CoordinateUtility.ChunkToWorldPosition(chunkPosition);

            // Check each column in the chunk
            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                // Get tree density for this position
                float density = baseTreeDensity;
                float noise = GetNoise(worldPos.x + x, worldPos.z + z, 0.02f);
                density *= Mathf.Lerp(0.5f, 1.5f, noise);

                if (random.NextDouble() >= density) continue;

                // Find highest grass block in this column
                for (int y = Chunk.ChunkSize - 1; y > 0; y--)
                {
                    if (chunk.GetBlock(x, y, z)?.Name != Block.Types.Grass.Name) continue;

                    // Check for 3 air blocks above
                    if (y + 3 >= Chunk.ChunkSize) continue;
                    if (chunk.GetBlock(x, y + 1, z) != null) continue;
                    if (chunk.GetBlock(x, y + 2, z) != null) continue;
                    if (chunk.GetBlock(x, y + 3, z) != null) continue;

                    // Place tree one block above grass
                    Vector3Int treePos = new Vector3Int(
                        worldPos.x + x,
                        (chunkPosition.y * Chunk.ChunkSize) + y,
                        worldPos.z + z
                    );
                    PlaceTree(treePos, random);
                    break;
                }
            }
        }

        private void PlaceTree(Vector3Int blockPosition, System.Random random)
        {
            // Convert block position to entity position
            // Note: We keep Y as is since it's already in block coordinates
            Vector3Int entityPosition = new Vector3Int(
                blockPosition.x,
                blockPosition.y + 1, // Just 1 block above ground to prevent z-fighting
                blockPosition.z
            );

            // Create tree aligned to block grid
            var treeData = EntityDataManager.Instance.CreateEntity(
                EntityType.Static, 
                entityPosition,
                Quaternion.Euler(90, 0, 0) // Fixed rotation for grid alignment
            );

            // Scale can still vary if desired
            float scale = random.Next(80, 120) / 100f;
            treeData.SetState($"scale:{scale}");
        }

        private float GetNoise(float x, float z, float scale)
        {
            float nx = (x + offsets[0]) * scale;
            float nz = (z + offsets[1]) * scale;
            return Mathf.PerlinNoise(nx, nz);
        }

        private int GetChunkSeed(Vector3Int chunkPos)
        {
            return chunkPos.x * 73856093 ^ 
                   chunkPos.y * 19349663 ^ 
                   chunkPos.z * 83492791;
        }
    }
}