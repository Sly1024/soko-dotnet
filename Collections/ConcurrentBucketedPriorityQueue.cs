using System.Threading;
using System.Threading.Tasks;

namespace soko.Collections;

public class ConcurrentBucketedPriorityQueue<T>
{
    public readonly ConcurrentAutoCreateList<ConcurrentExpandingQueue<T>> buckets;
    public int lowestPriority;
    private int count = 0;

    public ConcurrentBucketedPriorityQueue(int initialMaxPriority = 100)
    {
        buckets = new(initialMaxPriority, () => new ConcurrentExpandingQueue<T>());
        lowestPriority = initialMaxPriority;
        Task.Factory.StartNew(LowestPriorityDetector, TaskCreationOptions.LongRunning);
    }

    public void Enqueue(T item, int priority)
    {
        buckets[priority].Enqueue(item);
        Interlocked.Increment(ref count);

        // If this priority is lower than the known min, try to update
        int current = lowestPriority;
        if (priority < current)
            Interlocked.CompareExchange(ref lowestPriority, priority, current);
    }

    public T Dequeue()
    {
        if (Count == 0)
        {
            return default!;
        }

        int initialLP = lowestPriority;

        for (int p = initialLP; p < buckets.Count; p++)
        {
            if (buckets[p].TryDequeue(out var result))
            {
                Interlocked.Decrement(ref count);

                // Update lowestPriority lazily 
                while (p < buckets.Count && buckets[p].ReservedCount == 0) p++;

                if (p > initialLP)
                    Interlocked.CompareExchange(ref lowestPriority, p, initialLP);

                return result;
            }
        }

        return default;
    }

    public int Count => Volatile.Read(ref count);

    public int lpd_counter = 0;
    private void LowestPriorityDetector()
    {
        while (true)
        {
            for (int p = 0; p < lowestPriority; ++p)
            {
                if (buckets[p].ReservedCount > 0)
                {
                    // try to lower hint
                    int cur = Volatile.Read(ref lowestPriority);
                    if (cur > p) Interlocked.CompareExchange(ref lowestPriority, p, cur);
                    lpd_counter++;
                    break;
                }
            }
            Thread.Sleep(100); // tune
        }
    }
}
