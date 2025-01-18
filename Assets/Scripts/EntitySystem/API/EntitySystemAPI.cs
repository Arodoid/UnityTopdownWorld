using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using EntitySystem.Core;
using EntitySystem.Core.Types;
using EntitySystem.Access;

namespace EntitySystem.API
{
    /// <summary>
    /// Primary interface for interacting with the Entity System.
    /// Provides a simplified, type-safe API for creating and managing entities.
    /// </summary>
    public class EntitySystemAPI
    {
        private readonly IEntitySystem _entitySystem;

        /// <summary>
        /// Initializes a new instance of the EntitySystemAPI.
        /// </summary>
        /// <param name="entitySystem">The underlying entity system implementation</param>
        public EntitySystemAPI(IEntitySystem entitySystem)
        {
            _entitySystem = entitySystem;
        }

        #region Entity Creation

        /// <summary>
        /// Creates a new entity at the specified position.
        /// </summary>
        /// <param name="entityType">The type of entity to create</param>
        /// <param name="position">The world position to spawn the entity</param>
        /// <returns>A wrapper for the created entity</returns>
        public EntityWrapper CreateEntity(string entityType, Vector3 position)
        {
            long entityId = _entitySystem.CreateEntity(entityType, position);
            return new EntityWrapper(entityId, _entitySystem);
        }

        #endregion

        #region Entity Queries

        /// <summary>
        /// Finds all entities within a specified radius of a position.
        /// </summary>
        /// <param name="position">Center position for the search</param>
        /// <param name="radius">Search radius in world units</param>
        /// <returns>Collection of entities within range</returns>
        public IEnumerable<EntityWrapper> GetEntitiesInRange(Vector3 position, float radius)
        {
            return _entitySystem.GetEntitiesInRange(position, radius)
                .Select(id => new EntityWrapper(id, _entitySystem));
        }

        /// <summary>
        /// Gets all entities with a specific component.
        /// </summary>
        /// <param name="componentType">The type of component to search for</param>
        /// <returns>Collection of entities with the specified component</returns>
        public IEnumerable<EntityWrapper> GetEntitiesWithComponent(string componentType)
        {
            return _entitySystem.GetEntitiesWithComponent(componentType)
                .Select(id => new EntityWrapper(id, _entitySystem));
        }

        #endregion

        #region Entity Management

        /// <summary>
        /// Destroys an entity and removes it from the world.
        /// </summary>
        /// <param name="entity">The entity to destroy</param>
        public void DestroyEntity(IEntityWrapper entity)
        {
            _entitySystem.DestroyEntity(entity.Id);
        }

        #endregion

        /// <summary>
        /// Gets an entity wrapper for the specified entity ID.
        /// </summary>
        /// <param name="entityId">The ID of the entity to get</param>
        /// <returns>An EntityWrapper for the specified entity</returns>
        public EntityWrapper GetEntity(long entityId)
        {
            return new EntityWrapper(entityId, _entitySystem);
        }
    }

    /// <summary>
    /// Base interface for all entity wrapper types.
    /// Provides common properties and methods for entities.
    /// </summary>
    public interface IEntityWrapper
    {
        /// <summary>
        /// Unique identifier for the entity
        /// </summary>
        long Id { get; }

        /// <summary>
        /// Current world position of the entity
        /// </summary>
        Vector3 Position { get; }
    }

    /// <summary>
    /// Generic wrapper for entities.
    /// Provides access to entity properties and components.
    /// </summary>
    public class EntityWrapper : IEntityWrapper
    {
        private readonly long _entityId;
        private readonly IEntitySystem _entitySystem;

        public long Id => _entityId;
        public Vector3 Position => _entitySystem.GetEntityPosition(_entityId);

        internal EntityWrapper(long entityId, IEntitySystem entitySystem)
        {
            _entityId = entityId;
            _entitySystem = entitySystem;
        }

        /// <summary>
        /// Sets a component value on the entity.
        /// </summary>
        /// <param name="componentType">The type of component to modify</param>
        /// <param name="value">The new value to set</param>
        public void SetComponentValue(string componentType, object value)
        {
            _entitySystem.SetComponentValue(_entityId, componentType, value);
        }

        /// <summary>
        /// Gets a component value from the entity.
        /// </summary>
        /// <typeparam name="T">The type of value to retrieve</typeparam>
        /// <param name="componentType">The type of component to get</param>
        /// <returns>The component value</returns>
        public T GetComponentValue<T>(string componentType)
        {
            return _entitySystem.GetComponentValue<T>(_entityId, componentType);
        }

        /// <summary>
        /// Checks if the entity has a specific component.
        /// </summary>
        /// <param name="componentType">The type of component to check for</param>
        /// <returns>True if the entity has the component</returns>
        public bool HasComponent(string componentType)
        {
            return _entitySystem.HasComponent(_entityId, componentType);
        }

        /// <summary>
        /// Sets the entity's state.
        /// </summary>
        /// <param name="state">The new state to set</param>
        public void SetState(EntityState state)
        {
            _entitySystem.SetEntityState(_entityId, state);
        }
    }
} 