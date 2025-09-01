using System;
using System.Threading;
namespace soko;


public class ConcurrentQueueExpanding<T>
{
    private T[] _buffer;

    private int _head;          // next item to dequeue
    private int _tailReserved;  // reserved slots (may not be written yet)
    private int _tailCommitted; // committed slots (definitely written)

    private readonly ReaderWriterLockSimple rwLock = new();

    public ConcurrentQueueExpanding(int initialCapacity = 32)
    {
        if ((initialCapacity & (initialCapacity - 1)) != 0)
            throw new ArgumentException("initialCapacity must be power-of-two");

        _buffer = new T[initialCapacity];
    }

    public int CommittedCount
    {
        get
        {
            int head = Volatile.Read(ref _head);
            int tailCommitted = Volatile.Read(ref _tailCommitted);
            return tailCommitted - head;
        }
    }

    public int ReservedCount
    {
        get
        {
            int head = Volatile.Read(ref _head);
            int tailReserved = Volatile.Read(ref _tailReserved);
            return tailReserved - head;
        }
    }
    public void Enqueue(T item)
    {
        rwLock.AcquireReadLock();
        while (true)
        {
            int head = Volatile.Read(ref _head);
            int tail = Volatile.Read(ref _tailReserved);

            if (tail - head >= _buffer.Length)
            {
                rwLock.ReleaseReadLock();
                Resize();
                rwLock.AcquireReadLock();
                continue;
            }

            // try reserve slot
            if (Interlocked.CompareExchange(ref _tailReserved, tail + 1, tail) != tail)
            {
                // lost race → spin + retry
                Thread.SpinWait(1);
                continue;
            }

            // got slot at index = tail & (capacity - 1)
            int idx = tail & (_buffer.Length - 1);
            _buffer[idx] = item;

            // commit
            while (Interlocked.CompareExchange(ref _tailCommitted, tail + 1, tail) != tail)
            {
                Thread.SpinWait(1);
            }
            break;
        }
        rwLock.ReleaseReadLock();
    }


    public bool TryDequeue(out T item)
    {
        bool success = false;
        item = default!;

        rwLock.AcquireReadLock();
        while (true)
        {
            int head = Volatile.Read(ref _head);
            int tailCommitted = Volatile.Read(ref _tailCommitted);

            if (head == tailCommitted) break;   // empty

            if (Interlocked.CompareExchange(ref _head, head + 1, head) != head)
            {
                Thread.SpinWait(1);
                continue;
            }

            int idx = head & (_buffer.Length - 1);
            item = _buffer[idx];
            _buffer[idx] = default!;
            success = true;
            break;
        }
        rwLock.ReleaseReadLock();

        return success;
    }

    private void Resize()
    {
        rwLock.AcquireWriteLock();

        int head = Volatile.Read(ref _head);
        int tailCommitted = Volatile.Read(ref _tailCommitted);
        int count = tailCommitted - head;

        var _capacity = _buffer.Length;

        if (count < _capacity * 3 / 4)
        {
            // another thread resized meanwhile → skip
            rwLock.ReleaseWriteLock();
            return;
        }

        int newCapacity = _capacity * 2;
        var newBuffer = new T[newCapacity];

        for (int i = 0; i < count; i++)
        {
            newBuffer[i] = _buffer[(head + i) & (_capacity - 1)];
        }

        _buffer = newBuffer;

        _head = 0;
        _tailReserved = count;
        _tailCommitted = count;

        rwLock.ReleaseWriteLock();
    }

}
