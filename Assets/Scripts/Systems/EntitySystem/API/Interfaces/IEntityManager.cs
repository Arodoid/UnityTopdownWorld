using Unity.Mathematics;
using System.Threading.Tasks;
using EntitySystem.Data;
using System.Collections.Generic;

namespace EntitySystem.API
{
    public interface IEntityManager
    {
        EntityHandle CreateEntity(string entityId, int3 position);
        bool DestroyEntity(EntityHandle handle);
        bool TryGetEntityPosition(EntityHandle handle, out int3 position);
        bool SetEntityPosition(EntityHandle handle, int3 position);
        IEnumerable<string> GetAvailableTemplates();
    }
} 