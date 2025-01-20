using UnityEngine;
using Unity.Mathematics;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZoneSystem.Data;

namespace ZoneSystem.API
{
    public class ZoneSystemAPI
    {
        private readonly IZoneManager _zoneManager;

        public ZoneSystemAPI(IZoneManager zoneManager)
        {
            _zoneManager = zoneManager;
        }

        public async Task<bool> CreateZone(int3 start, int3 end, string zoneType)
        {
            Debug.Log($"ZoneSystemAPI CreateZone called with type {zoneType}");
            if (System.Enum.TryParse<ZoneType>(zoneType, out var type))
            {
                Debug.Log($"Successfully parsed zone type {zoneType} to enum {type}");
                var result = await _zoneManager.CreateZone(start, end, type);
                Debug.Log($"Zone creation result: {result}");
                return result;
            }
            Debug.LogError($"Failed to parse zone type: {zoneType}");
            return false;
        }

        public async Task<bool> RemoveZone(int3 start, int3 end, string zoneType)
        {
            if (System.Enum.TryParse<ZoneType>(zoneType, out var type))
            {
                return await _zoneManager.RemoveZone(start, end, type);
            }
            return false;
        }

        public IEnumerable<string> GetAvailableZoneTypes()
        {
            return System.Enum.GetNames(typeof(ZoneType));
        }

        // TODO: Get all zones
        // TODO: Get zones by type
        // TODO: Get zones at position
        // TODO: Get zone attributes
        // TODO: Modify zone attributes
        // TODO: Check if position is in zone
        // TODO: Get zone boundaries
        // TODO: Get zone volume
        // TODO: Get closest zone of type
    }
} 