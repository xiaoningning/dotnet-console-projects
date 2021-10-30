using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

public class PriorityBlockingCollection<TElement> : IProducerConsumerCollection<TElement>
{
    PriorityQueue<TElement, TElement> _priorityQueue;
    readonly object _lock = new object();
    readonly int _boundedCapacity = -1;
    List<TElement> _queueCopy;
    public PriorityBlockingCollection()
    {
        _priorityQueue = new PriorityQueue<TElement, TElement>();
        _queueCopy = new List<TElement>();
    }
    public PriorityBlockingCollection(int boundedCapacity)
    {
        _priorityQueue = new PriorityQueue<TElement, TElement>(boundedCapacity);
        _boundedCapacity = boundedCapacity;
        _queueCopy = new List<TElement>();
    }
    public PriorityBlockingCollection(IComparer<TElement> comparer)
    {
        _priorityQueue = new PriorityQueue<TElement, TElement>(comparer);
        _queueCopy = new List<TElement>();
    }
    public PriorityBlockingCollection(int boundedCapacity, IComparer<TElement> comparer)
    {
        _priorityQueue = new PriorityQueue<TElement, TElement>(boundedCapacity, comparer);
        _queueCopy = new List<TElement>();
        _boundedCapacity = boundedCapacity;
    }
    public PriorityBlockingCollection(IEnumerable<(TElement, TElement)> items, IComparer<TElement> comparer)
    {
        _priorityQueue = new PriorityQueue<TElement, TElement>(items, comparer);
        _queueCopy = new List<TElement>();
    }
    public TElement Peek()
    {
        TElement t;
        lock (_lock)
        {
            if (_priorityQueue.Count == 0) t = default(TElement);
            else t = _priorityQueue.Peek();
        }
        return t;
    }
    public void Enqueue(TElement item)
    {
        lock (_lock) _priorityQueue.Enqueue(item, item);
    }
    public bool TryEnqueue(TElement item)
    {
        bool rval = true;
        if (_boundedCapacity != -1 && _priorityQueue.Count >= _boundedCapacity) rval = false;
        else Enqueue(item);
        return rval;
    }
    public bool TryDequeue(out TElement item)
    {
        bool rval = true;
        lock (_lock)
        {
            if (_priorityQueue.Count == 0) { item = default(TElement); rval = false; }
            else item = _priorityQueue.Dequeue();
        }
        return rval;
    }

    public bool TryTake(out TElement item)
    {
        return TryDequeue(out item);
    }

    public bool TryAdd(TElement item)
    {
        Enqueue(item);
        return true;
    }
    public TElement[] ToArray()
    {
        TElement[] rval = new TElement[_priorityQueue.Count];
        lock (_lock) rval = _priorityQueue.UnorderedItems.Select(x => x.Element).ToArray();
        return rval;
    }

    public IEnumerator<TElement> GetEnumerator()
    {
        _queueCopy = new List<TElement>(ToArray());
        return _queueCopy.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return _queueCopy.GetEnumerator();
    }

    public void CopyTo(TElement[] array, int index)
    {
        lock (_lock) ToArray().CopyTo(array, index);
    }
    public bool IsSynchronized
    {
        get { return true; }
    }

    public object SyncRoot
    {
        get { return _priorityQueue.Count; }
    }

    public int Count
    {
        get { return _priorityQueue.Count; }
    }
    public void CopyTo(Array array, int index)
    {
        throw new NotImplementedException();
    }
}