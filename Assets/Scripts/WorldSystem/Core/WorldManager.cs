using UnityEngine;
using System;
using System.IO;

namespace WorldSystem
{
    [Serializable]
    public class WorldMetadata
    {
        public string worldName;
        public int seed;
        public long created;
        public long lastPlayed;
    }

    public class WorldManager : MonoBehaviour
    {
        private static string WorldsPath => 
            Path.Combine(Application.persistentDataPath, "Worlds");

        public static WorldMetadata CreateWorld(string worldName)
        {
            var metadata = new WorldMetadata
            {
                worldName = worldName,
                seed = UnityEngine.Random.Range(0, 99999),
                created = DateTime.Now.Ticks,
                lastPlayed = DateTime.Now.Ticks
            };

            string worldPath = Path.Combine(WorldsPath, worldName);
            Directory.CreateDirectory(worldPath);
            
            string metadataPath = Path.Combine(worldPath, "world.json");
            File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata));

            return metadata;
        }

        public static WorldMetadata LoadWorld(string worldName)
        {
            string metadataPath = Path.Combine(WorldsPath, worldName, "world.json");
            if (!File.Exists(metadataPath))
                return null;

            string json = File.ReadAllText(metadataPath);
            return JsonUtility.FromJson<WorldMetadata>(json);
        }

        public static void UpdateLastPlayed(string worldName)
        {
            var metadata = LoadWorld(worldName);
            if (metadata != null)
            {
                metadata.lastPlayed = DateTime.Now.Ticks;
                string metadataPath = Path.Combine(WorldsPath, worldName, "world.json");
                File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata));
            }
        }
    }
} 