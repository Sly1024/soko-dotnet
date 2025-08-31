using System.Threading;
using System.Threading.Tasks;

namespace soko
{

    public class ConcurrentBucketedPriorityQueue<T>
    {
        private readonly ConcurrentAutoCreateList<SokoConcurrentQueue<T>> buckets;
        public int lowestPriority;
        private int count = 0;

        public ConcurrentBucketedPriorityQueue(int numMaxThreads, int initialMaxPriority = 100)
        {
            buckets = new(initialMaxPriority, () => new SokoConcurrentQueue<T>(numMaxThreads));
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
            if (Interlocked.CompareExchange(ref count, 0, 0) == 0)
            {
                return default!;
            }

            for (int p = lowestPriority; p < buckets.Count; p++)
            {
                if (buckets[p].TryDequeue(out var result))
                {
                    Interlocked.Decrement(ref count);
                    // Update lowestPriority lazily 
                    if (buckets[p].Count == 0)
                    {
                        // Move forward until next non-empty
                        int newP = p;
                        while (newP < buckets.Count && buckets[newP].Count == 0) newP++;
                        Interlocked.CompareExchange(ref lowestPriority, newP, p);
                    }
                    return result;
                }
            }

            return default;
        }

        public int Count => Interlocked.CompareExchange(ref count, 0, 0);

        public int lpd_counter = 0;
        private void LowestPriorityDetector()
        {
            while (true)
            {
                for (int p = 0; p < lowestPriority; ++p)
                {
                    if (buckets[p].Count > 0)
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
}