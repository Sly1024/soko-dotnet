using System.Text;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace soko
{

    public class ConcurrentBucketedPriorityQueue<T>
    {
        private readonly ConcurrentAutoCreateList<SokoConcurrentQueue<T>> buckets;
        private int lowestPriority;
        private int count = 0;

        public ConcurrentBucketedPriorityQueue(int numMaxThreads, int initialMaxPriority = 100)
        {
            buckets = new(initialMaxPriority, () => new SokoConcurrentQueue<T>(numMaxThreads));
            lowestPriority = initialMaxPriority;
            // Task.Factory.StartNew(RepairWorker, TaskCreationOptions.LongRunning);
        }

        public void Enqueue(T item, int priority)
        {
            buckets[priority].Enqueue(item);
            Interlocked.Increment(ref count);

            // If this priority is lower than the known min, try to update
            int current;
            while ((current = lowestPriority) > priority)
                Interlocked.CompareExchange(ref lowestPriority, priority, current);
        }

        public T Dequeue()
        {
            while (true)
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
                        return result;
                    }
                    Interlocked.CompareExchange(ref lowestPriority, p + 1, p);
                }
            }

            // return default;
        }

        public int Count => Interlocked.CompareExchange(ref count, 0, 0);

        private void RepairWorker()
        {
            while (true)
            {                
                for (int p = 0; p < Volatile.Read(ref lowestPriority); ++p)
                {
                    if (buckets[p].Count > 0)
                    {
                        // try to lower hint
                        int cur = Volatile.Read(ref lowestPriority);
                        if (cur > p) Interlocked.CompareExchange(ref lowestPriority, p, cur);
                        break;
                    }
                }
                Thread.Sleep(10); // tune
            }
        }
    }
}