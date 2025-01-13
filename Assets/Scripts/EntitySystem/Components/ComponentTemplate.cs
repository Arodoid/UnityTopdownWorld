using UnityEngine;
using EntitySystem.Core;
using EntitySystem.Core.Interfaces;

public abstract class GameComponent : EntityComponent
{
    protected Transform Transform => Entity.GameObject.transform;
    protected Vector3 Position => Entity.Position;
    
    public T Get<T>() where T : class, IEntityComponent 
        => Entity.GetComponent<T>();
        
    protected T Self<T>() where T : GameComponent => (T)this;
} 