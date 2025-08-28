using System.Threading;

namespace soko
{
    public class ReaderWriterLockFast
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
            while (true)
            {
                // Fast-path: if there are no writers waiting AND no active writer, try to join readers.
                // We check both with Volatile.Read to avoid reordering.
                if (Volatile.Read(ref _writersWaiting) == 0 && Volatile.Read(ref _writerActive) == 0)
                {
                    // Increment readers count atomically.
                    Interlocked.Increment(ref _readers);

                    // Now re-check that no writer sneaked in between our checks and the increment.
                    // If a writer is waiting or active, we must back off and undo this increment.
                    if (Volatile.Read(ref _writersWaiting) == 0 && Volatile.Read(ref _writerActive) == 0)
                    {
                        // Success: we hold the read lock.
                        return;
                    }

                    // A writer appeared: back out and retry.
                    Interlocked.Decrement(ref _readers);
                }

                // backoff
                spins++;
                if (spins < SPIN_BEFORE_YIELD) Thread.SpinWait(4 << (spins & 7));
                else Thread.Yield();
            }
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
}