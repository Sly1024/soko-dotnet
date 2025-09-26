using System.Threading;

namespace soko.Collections;

public class ReaderWriterLockSimple
{
    // number of active readers (>=0)
    private int _readers = 0;

    // number of writers waiting (>=0). When >0, new readers must not start.
    private int _writersWaiting = 0;

    // 0 = no active writer, 1 = active writer
    private int _writerActive = 0;

    // tuning: how many spin iterations before yielding
    private const int SPIN_BEFORE_YIELD = 50;

    public void AcquireReadLock()
    {
        int spins = 0;
        // if there are writers waiting OR an active writer, we spin.
        while (Volatile.Read(ref _writersWaiting) != 0)
        {
            spins++;
            if (spins < SPIN_BEFORE_YIELD) Thread.SpinWait(4 << (spins & 7));
            else Thread.Yield();
        }
        Interlocked.Increment(ref _readers);
    }

    public void ReleaseReadLock()
    {
        // simple decrement; readers should not go negative
        Interlocked.Decrement(ref _readers);
    }

    public void AcquireWriteLock()
    {
        // Announce we're waiting. This prevents new readers from starting.
        Interlocked.Increment(ref _writersWaiting);

        int spins = 0;
        // Acquire exclusive writerActive flag (only one writer may hold it).
        while (true)
        {
            // Try to become the active writer (0 -> 1)
            if (Interlocked.CompareExchange(ref _writerActive, 1, 0) == 0)
            {
                // Wait for all active readers to drain
                while (Volatile.Read(ref _readers) != 0)
                {
                    // spin until readers go away
                    spins++;
                    if (spins < SPIN_BEFORE_YIELD) Thread.SpinWait(4 << (spins & 7));
                    else Thread.Yield();
                }
                // We are the active writer and no readers remain.
                return;
            }

            // Someone else is the active writer; wait for them to finish
            spins++;
            if (spins < SPIN_BEFORE_YIELD) Thread.SpinWait(4 << (spins & 7));
            else Thread.Yield();
        }
    }

    public void ReleaseWriteLock()
    {
        // Release writerActive and allow others to proceed.
        Volatile.Write(ref _writerActive, 0);

        // We are no longer waiting as a writer (decrement waiting count).
        Interlocked.Decrement(ref _writersWaiting);
    }
}
