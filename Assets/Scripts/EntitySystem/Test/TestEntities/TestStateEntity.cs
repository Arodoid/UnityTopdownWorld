using UnityEngine;
using EntitySystem.Core;
using EntitySystem.Core.Interfaces;
using EntitySystem.Core.Types;

// Changed to be a concrete class that can be instantiated
public class TestStateEntity : Entity
{
    public TestStateEntity(long id, EntityManager manager) : base(id, manager)
    {
    }

    protected override void SetupComponents()
    {
        AddComponent<TestStateComponent>();
        AddComponent<TestPositionComponent>();
    }
}

// Component that reacts to state changes
public class TestStateComponent : EntityComponent, IStateAwareComponent
{
    private Material _material;
    private static readonly Color 
        ActiveColor = Color.green,
        InactiveColor = Color.red,
        TransitionColor = Color.yellow,
        PooledColor = Color.gray;

    protected override void OnInitialize(Entity entity)
    {
        // Add visual representation
        var renderer = Entity.GameObject.AddComponent<MeshRenderer>();
        var filter = Entity.GameObject.AddComponent<MeshFilter>();
        filter.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        
        _material = new Material(Shader.Find("Standard"));
        renderer.material = _material;
        
        UpdateColor(Entity.State);
    }

    public void OnStateChanged(EntityState oldState, EntityState newState)
    {
        UpdateColor(newState);
    }

    private void UpdateColor(EntityState state)
    {
        Color color = state switch
        {
            EntityState.Active => ActiveColor,
            EntityState.Inactive => InactiveColor,
            EntityState.Transitioning => TransitionColor,
            EntityState.Pooled => PooledColor,
            _ => Color.white
        };
        
        _material.color = color;
    }
}

// Component that reacts to position changes
public class TestPositionComponent : EntityComponent, IPositionAwareComponent
{
    private Vector3 _lastPosition;
    private LineRenderer _pathRenderer;

    protected override void OnInitialize(Entity entity)
    {
        _lastPosition = entity.Position;
        
        // Add line renderer for movement trail
        _pathRenderer = Entity.GameObject.AddComponent<LineRenderer>();
        _pathRenderer.startWidth = 0.1f;
        _pathRenderer.endWidth = 0.1f;
        _pathRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _pathRenderer.startColor = Color.blue;
        _pathRenderer.endColor = Color.blue;
    }

    public void OnPositionChanged(Vector3 oldPosition, Vector3 newPosition)
    {
        
        // Update trail
        _pathRenderer.positionCount += 1;
        _pathRenderer.SetPosition(_pathRenderer.positionCount - 1, newPosition);
    }
} 