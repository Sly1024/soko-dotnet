using System;
using System.Threading;

namespace soko
{
    public class ConcurrentAutoCreateList<T>(int capacity, Func<T> factory) where T : class
    {
        private readonly Func<T> factory = factory;
        private T[] items = new T[capacity];
        private int count = 0;
        private readonly ReaderWriterLockSimple rwl = new();

        public int Count => count;

        public T this[int idx]
        {
            get
            {
                if (items.Length < idx + 1) EnsureCapacity(idx + 1);

                var item = items[idx];
                if (item != null) return item;
                
                rwl.AcquireReadLock();
                Interlocked.CompareExchange(ref items[idx], factory(), null);

                while (true)
                {
                    var current_count = count;
                    if (idx + 1 <= current_count) break;
                    if (Interlocked.CompareExchange(ref count, idx + 1, current_count) == current_count) break;
                }

                rwl.ReleaseReadLock();

                return items[idx];
            }
        }
        
        public void EnsureCapacity(int size)
        {
            // if (items.Length >= size) return; - no need, we check it before calling EnsureCapacity
            rwl.AcquireWriteLock();
            if (items.Length < size)
            {
                var newItems = new T[Math.Max(items.Length * 3 / 2, size)];
                Array.Copy(items, newItems, count);
                items = newItems;
            }
            rwl.ReleaseWriteLock();
        }
    }
}