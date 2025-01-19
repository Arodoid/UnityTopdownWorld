using System.Collections.Generic;

namespace WorldSystem.Data
{
    public class PriorityQueue<T>
    {
        private readonly List<(T item, float priority)> _items = new();
        private readonly HashSet<T> _itemSet = new();
        private bool _isDirty = false;

        public void Enqueue(T item, float priority)
        {
            if (_itemSet.Add(item))
            {
                _items.Add((item, priority));
                _isDirty = true;
            }
        }

        public T Peek()
        {
            if (_items.Count == 0) throw new System.InvalidOperationException("Queue is empty");
            
            if (_isDirty)
            {
                _items.Sort((a, b) => a.priority.CompareTo(b.priority));
                _isDirty = false;
            }

            return _items[0].item;
        }

        public T Dequeue()
        {
            if (_items.Count == 0) throw new System.InvalidOperationException("Queue is empty");
            
            if (_isDirty)
            {
                _items.Sort((a, b) => a.priority.CompareTo(b.priority));
                _isDirty = false;
            }

            T item = _items[0].item;
            _items.RemoveAt(0);
            _itemSet.Remove(item);
            return item;
        }

        public int Count => _items.Count;

        public bool Contains(T item)
        {
            return _itemSet.Contains(item);
        }

        public void Clear()
        {
            _items.Clear();
            _itemSet.Clear();
            _isDirty = false;
        }
    }
}