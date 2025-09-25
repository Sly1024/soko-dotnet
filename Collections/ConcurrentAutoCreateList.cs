using System;
using System.Threading;

namespace soko.Collections;

public class ConcurrentAutoCreateList<T>(int capacity, Func<T> factory) where T : class
{
    private readonly Func<T> factory = factory;
    private T[] items = new T[capacity];
    private int count = 0;
    private readonly ReaderWriterLockSimple rwl = new();

    public int Count => Volatile.Read(ref count);

    public T this[int idx]
    {
        get
        {
            if (items.Length < idx + 1) Resize(idx + 1);

            var item = items[idx];
            if (item != null) return item;
            
            rwl.AcquireReadLock();
            Interlocked.CompareExchange(ref items[idx], factory(), null);

            while (true)
            {
                var current_count = Count;
                if (idx + 1 <= current_count) break;
                if (Interlocked.CompareExchange(ref count, idx + 1, current_count) == current_count) break;
            }

            rwl.ReleaseReadLock();

            return Volatile.Read(ref items[idx]);
        }
    }
    
    public void Resize(int size)
    {
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
