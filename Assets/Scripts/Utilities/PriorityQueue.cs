using System;
using System.Collections.Generic;
using System.Linq;

public class PriorityQueue<T>
{
    private readonly SortedDictionary<int, Queue<T>> _priorityDict = new SortedDictionary<int, Queue<T>>();
    private int _count = 0;

    /// <summary>
    /// Adds an item to the priority queue with the specified priority.
    /// </summary>
    /// <param name="item">The item to add to the queue.</param>
    /// <param name="priority">The priority of the item (lower values represent higher priority).</param>
    public void Enqueue(T item, int priority)
    {
        if (!_priorityDict.ContainsKey(priority))
        {
            _priorityDict[priority] = new Queue<T>();
        }

        _priorityDict[priority].Enqueue(item);
        _count++;
    }

    /// <summary>
    /// Removes and returns the highest-priority item (lowest priority value).
    /// </summary>
    /// <returns>The item with the highest priority.</returns>
    public T Dequeue()
    {
        if (_count == 0)
        {
            throw new InvalidOperationException("The priority queue is empty.");
        }

        // Get the first key in the sorted dictionary (highest priority)
        var firstKey = GetHighestPriorityKey();
        var queue = _priorityDict[firstKey];

        T item = queue.Dequeue();
        _count--;

        // If the queue for this priority is empty, remove it
        if (queue.Count == 0)
        {
            _priorityDict.Remove(firstKey);
        }

        return item;
    }

    /// <summary>
    /// Returns the number of items in the queue.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Helper method to get the key with the highest priority.
    /// </summary>
    private int GetHighestPriorityKey()
    {
        foreach (var key in _priorityDict.Keys)
        {
            return key; // As SortedDictionary maintains keys sorted, this is the highest priority
        }

        throw new InvalidOperationException("No priorities found in the queue.");
    }

    /// <summary>
    /// Removes the specified item from the priority queue.
    /// </summary>
    /// <param name="item">The item to remove from the queue.</param>
    /// <returns>True if the item was found and removed; otherwise, false.</returns>
    public bool Remove(T item)
    {
        foreach (var queue in _priorityDict.Values)
        {
            // Create a new queue without the item
            var newQueue = new Queue<T>(queue.Where(x => !x.Equals(item)));
            
            if (newQueue.Count < queue.Count)
            {
                // Item was found and removed
                _count--;
                
                // Replace the old queue with the filtered one
                queue.Clear();
                foreach (var remaining in newQueue)
                {
                    queue.Enqueue(remaining);
                }
                
                return true;
            }
        }
        return false;
    }
}