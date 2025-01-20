using Unity.Mathematics;
using ZoneSystem.Data;
using System.Threading.Tasks;

namespace ZoneSystem.API
{
    public interface IZoneManager
    {
        Task<bool> CreateZone(int3 start, int3 end, ZoneType zoneType);
        Task<bool> RemoveZone(int3 start, int3 end, ZoneType zoneType);
    }
} 