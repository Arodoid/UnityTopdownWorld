using UnityEngine;
using System.Threading.Tasks;
using VoxelGame.WorldSystem.Biomes;
using VoxelGame.WorldSystem.Generation.Terrain;
using VoxelGame.Core.Debugging;

namespace VoxelGame.WorldSystem.Generation.Core
{
    public class WorldGenerator : MonoBehaviour
    {
        [SerializeField] private int worldSeed;
        
        private NoiseGenerator noiseGenerator;
        private BiomeGenerator biomeGenerator;
        private TerrainGenerator terrainGenerator;

        public BiomeGenerator BiomeGenerator => biomeGenerator;

        private void Awake()
        {
            InitializeGenerators();
        }

        private void InitializeGenerators()
        {
            if (worldSeed == 0)
                worldSeed = Random.Range(int.MinValue, int.MaxValue);
            
            noiseGenerator = new NoiseGenerator(worldSeed);
            
            // Get or add BiomeGenerator component
            biomeGenerator = GetComponent<BiomeGenerator>();
            if (biomeGenerator == null)
                biomeGenerator = gameObject.AddComponent<BiomeGenerator>();
            biomeGenerator.Initialize(noiseGenerator);
            
            terrainGenerator = new TerrainGenerator(noiseGenerator);

            GameLogger.LogInfo($"WorldGenerator initialized with seed: {worldSeed}");
        }

        public Task<Chunk> GenerateChunk(Vector3Int position)
        {
            var chunk = new Chunk(position);
            var biomeData = biomeGenerator.GenerateChunkBiomeData(position);
            var heightMap = terrainGenerator.GenerateChunkTerrain(chunk, biomeData);
            return Task.FromResult(chunk);
        }
    }
} 