using System;
using System.Threading;

namespace soko;

public class SokoConcurrentQueue<T>
{
    private T[] buffer;
    private int sizeMask;
    private readonly ReaderWriterLockSimple rwLock = new();
    private readonly int numMaxThreads;

    private record struct HeadTail(int head, int tail)
    {
        public int head = head;
        public int tail = tail;

        public readonly int Count => tail - head;

        public static implicit operator HeadTail(ulong ht) => new() { head = (int)(ht >> 32), tail = (int)ht };
        public static implicit operator ulong(HeadTail ht) => (((ulong)ht.head) << 32) | (uint)ht.tail;
    }

    private ulong headTail = 0;

    public SokoConcurrentQueue(int numMaxThreads, int capacity = 1024)
    {
        if ((capacity & (capacity - 1)) != 0) throw new ArgumentException("capacity must be a power-of-two");
        this.numMaxThreads = numMaxThreads;
        buffer = new T[capacity];
        sizeMask = capacity - 1;
    }

    public int Count => ((HeadTail)Volatile.Read(ref headTail)).Count;

    public void Enqueue(T item)
    {
        rwLock.AcquireReadLock(); // shared
        while (true)
        {
            ulong oldHt = Volatile.Read(ref headTail);
            HeadTail ht = oldHt;

            if (ht.Count >= buffer.Length)
            {
                rwLock.ReleaseReadLock();
                Resize();
                rwLock.AcquireReadLock();
                continue;
            }

            ulong newHT = new HeadTail(ht.head, ht.tail + 1);
            if (Interlocked.CompareExchange(ref headTail, newHT, oldHt) == oldHt)
            {
                buffer[ht.tail & sizeMask] = item;
                break;
            }
        }
        rwLock.ReleaseReadLock();
    }

    public bool TryDequeue(out T value)
    {
        bool success;

        var isWriteLockHeld = false; 

        rwLock.AcquireReadLock(); // shared
        while (true)
        {
            ulong oldHt = Volatile.Read(ref headTail);
            HeadTail ht = oldHt;

            if (ht.Count == 0) { value = default; success = false; break; }

            if (ht.Count <= numMaxThreads && !isWriteLockHeld)
            {
                rwLock.ReleaseReadLock();
                rwLock.AcquireWriteLock();
                isWriteLockHeld = true;
                continue;
            }

            ulong newHT = new HeadTail(ht.head + 1, ht.tail);
            if (Interlocked.CompareExchange(ref headTail, newHT, oldHt) == oldHt)
            {
                value = buffer[ht.head & sizeMask]!;
                success = true;
                break;
            }
        }
        if (isWriteLockHeld) rwLock.ReleaseWriteLock(); else rwLock.ReleaseReadLock();
        return success;
    }

    private void Resize()
    {
        rwLock.AcquireWriteLock();

        HeadTail ht = headTail;
        var count = ht.Count;

        if (count >= buffer.Length * 3 / 4) // above 75%
        {
            var newBuf = new T[buffer.Length * 2];

            for (int i = 0; i < count; i++)
            {
                newBuf[i] = buffer[(ht.head + i) & sizeMask];
            }

            buffer = newBuf;
            sizeMask = buffer.Length - 1;
            headTail = new HeadTail(0, count);
        }
        rwLock.ReleaseWriteLock();
    }
}
