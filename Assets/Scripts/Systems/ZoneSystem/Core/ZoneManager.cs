using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using ZoneSystem.API;
using ZoneSystem.Data;

namespace ZoneSystem.Core
{
    public class ZoneManager : MonoBehaviour, IZoneManager
    {
        private Dictionary<ZoneType, List<Zone>> _zones = new Dictionary<ZoneType, List<Zone>>();
        private Transform _zonesParent;

        private void Awake()
        {
            Debug.Log("ZoneManager Awake");
            // Create a parent object for all zones
            _zonesParent = new GameObject("Zones").transform;
            _zonesParent.SetParent(transform);
        }

        public Task<bool> CreateZone(int3 start, int3 end, ZoneType zoneType)
        {
            Debug.Log($"ZoneManager creating zone of type {zoneType}");
            var positions = GetPositionsInBox(start, end);
            Debug.Log($"Got {positions.Count()} positions for zone");
            
            // Check if we can merge with existing zone
            if (_zones.TryGetValue(zoneType, out var zoneList))
            {
                Debug.Log($"Found existing zone list for type {zoneType} with {zoneList.Count} zones");
                foreach (var existingZone in zoneList)
                {
                    if (HasOverlap(positions, existingZone))
                    {
                        Debug.Log("Found overlapping zone, merging");
                        existingZone.AddPositions(positions);
                        return Task.FromResult(true);
                    }
                }
            }
            else
            {
                Debug.Log($"Creating new zone list for type {zoneType}");
                _zones[zoneType] = new List<Zone>();
            }

            // Create new zone as child of zones parent
            Debug.Log("Creating new zone GameObject");
            var zoneObject = new GameObject($"Zone_{zoneType}_{_zones[zoneType].Count}");
            zoneObject.transform.SetParent(_zonesParent);
            var newZone = zoneObject.AddComponent<Zone>();
            newZone.Initialize(zoneType);
            newZone.AddPositions(positions);
            _zones[zoneType].Add(newZone);
            
            Debug.Log($"Zone created successfully. Total zones of type {zoneType}: {_zones[zoneType].Count}");
            return Task.FromResult(true);
        }

        public Task<bool> RemoveZone(int3 start, int3 end, ZoneType zoneType)
        {
            var positions = GetPositionsInBox(start, end);
            
            if (_zones.TryGetValue(zoneType, out var zoneList))
            {
                foreach (var existingZone in zoneList.ToArray()) // Create copy to modify during iteration
                {
                    existingZone.RemovePositions(positions);
                    
                    if (existingZone.IsEmpty)
                    {
                        zoneList.Remove(existingZone);
                        Destroy(existingZone.gameObject);
                    }
                }
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }

        private IEnumerable<int3> GetPositionsInBox(int3 start, int3 end)
        {
            var positions = new List<int3>();
            int3 min = new int3(
                Mathf.Min(start.x, end.x),
                Mathf.Min(start.y, end.y),
                Mathf.Min(start.z, end.z)
            );
            int3 max = new int3(
                Mathf.Max(start.x, end.x),
                Mathf.Max(start.y, end.y),
                Mathf.Max(start.z, end.z)
            );

            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                    for (int z = min.z; z <= max.z; z++)
                        positions.Add(new int3(x, y, z));

            return positions;
        }

        private bool HasOverlap(IEnumerable<int3> positions, Zone zone)
        {
            foreach (var pos in positions)
            {
                if (zone.ContainsPosition(pos))
                    return true;
            }
            return false;
        }
    }
} 