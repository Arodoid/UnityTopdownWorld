using System.Collections.Generic;

public class PriorityQueue<TItem, TPriority> where TPriority : System.IComparable<TPriority>
{
    private List<(TItem item, TPriority priority)> heap;
    private HashSet<TItem> itemSet;  // Track items in queue

    public int Count => heap.Count;

    public PriorityQueue()
    {
        heap = new List<(TItem, TPriority)>();
        itemSet = new HashSet<TItem>();
    }

    public bool Contains(TItem item) => itemSet.Contains(item);

    public void Enqueue(TItem item, TPriority priority)
    {
        // Don't add if already in queue
        if (itemSet.Contains(item))
            return;
            
        heap.Add((item, priority));
        itemSet.Add(item);
        HeapifyUp(heap.Count - 1);
    }

    public TItem Dequeue()
    {
        if (heap.Count == 0)
            throw new System.InvalidOperationException("Queue is empty");

        TItem item = heap[0].item;
        
        // Move last item to root and remove last element
        int lastIndex = heap.Count - 1;
        heap[0] = heap[lastIndex];
        heap.RemoveAt(lastIndex);

        itemSet.Remove(item);  // Remove from tracking set

        if (heap.Count > 0)
            HeapifyDown(0);

        return item;
    }

    public bool TryPeek(out TItem item, out TPriority priority)
    {
        if (heap.Count > 0)
        {
            item = heap[0].item;
            priority = heap[0].priority;
            return true;
        }

        item = default;
        priority = default;
        return false;
    }

    private void HeapifyUp(int index)
    {
        while (index > 0)
        {
            int parentIndex = (index - 1) / 2;
            
            if (heap[parentIndex].priority.CompareTo(heap[index].priority) <= 0)
                break;
                
            // Swap with parent
            (heap[index], heap[parentIndex]) = (heap[parentIndex], heap[index]);
            index = parentIndex;
        }
    }

    private void HeapifyDown(int index)
    {
        while (true)
        {
            int smallest = index;
            int leftChild = 2 * index + 1;
            int rightChild = 2 * index + 2;

            if (leftChild < heap.Count && 
                heap[leftChild].priority.CompareTo(heap[smallest].priority) < 0)
                smallest = leftChild;

            if (rightChild < heap.Count && 
                heap[rightChild].priority.CompareTo(heap[smallest].priority) < 0)
                smallest = rightChild;

            if (smallest == index)
                break;

            // Swap with smallest child
            (heap[index], heap[smallest]) = (heap[smallest], heap[index]);
            index = smallest;
        }
    }
} 