namespace VoxelGame.Utilities
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public class ObjectPool<T>
    {
        private readonly Stack<T> pool;
        private readonly Func<T> createFunc;
        private readonly Action<T> onGet;
        private readonly Action<T> onRelease;
        private readonly int maxSize;

        public ObjectPool(Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null, int initialSize = 0, int maxSize = 1000)
        {
            this.pool = new Stack<T>(initialSize);
            this.createFunc = createFunc;
            this.onGet = onGet;
            this.onRelease = onRelease;
            this.maxSize = maxSize;

            // Pre-populate pool
            for (int i = 0; i < initialSize; i++)
            {
                Release(createFunc());
            }
        }

        public T Get()
        {
            T item = pool.Count > 0 ? pool.Pop() : createFunc();
            onGet?.Invoke(item);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            if (pool.Count >= maxSize) return;

            onRelease?.Invoke(item);
            pool.Push(item);
        }

        public void Clear()
        {
            pool.Clear();
        }
    }
} 