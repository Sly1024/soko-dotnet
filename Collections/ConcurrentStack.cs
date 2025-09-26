using System;
using System.Threading;

namespace soko.Collections;

public class ConcurrentStack<T> where T: class
{
    private int count = 0;
    private readonly ReaderWriterLockSimple rwLock = new();
    private T[] buffer;

    public int Count => Volatile.Read(ref count);

    public ConcurrentStack(int initialCapacity = 32)
    {
        if ((initialCapacity & (initialCapacity - 1)) != 0)
            throw new ArgumentException("initialCapacity must be power-of-two");

        buffer = new T[initialCapacity];
    }

    public void Push(T item)
    {
        rwLock.AcquireReadLock();
        while (true)
        {
            int pos = Volatile.Read(ref count);
            if (pos >= buffer.Length)
            {
                rwLock.ReleaseReadLock();
                Resize();
                rwLock.AcquireReadLock();
                continue;
            }

            // try to claim slot pos by installing item only if slot is null
            // CompareExchange returns the previous value: if it was null we succeeded
            if (Interlocked.CompareExchange(ref buffer[pos], item, null) == null)
            {
                // publication: increment tail so consumers can see the new element
                Interlocked.Increment(ref count);
                break;
            }

            // someone else wrote this slot (rare) — spin a bit and retry
            Thread.SpinWait(1);
        }
        rwLock.ReleaseReadLock();
    }

    public bool TryPop(out T value)
    {
        rwLock.AcquireReadLock();
        while (true)
        {
            int pos = Volatile.Read(ref count);
            if (pos == 0)
            {
                value = default;
                rwLock.ReleaseReadLock();
                return false;
            }

            T tmp = buffer[pos - 1]; // speculative read
            if (Interlocked.CompareExchange(ref count, pos - 1, pos) == pos)
            {
                value = tmp;
                rwLock.ReleaseReadLock();
                return true;
            }

            // failed, retry
        }
    }

    private void Resize()
    {
        rwLock.AcquireWriteLock();
        if (count >= buffer.Length) // recheck under exclusive lock
        {
            var newBuf = new T[buffer.Length * 2];
            Array.Copy(buffer, newBuf, buffer.Length);
            buffer = newBuf;
        }
        rwLock.ReleaseWriteLock();
    }

}