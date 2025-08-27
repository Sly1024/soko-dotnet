using System.Threading.Tasks;
using System.IO;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace soko
{
    class Program
    {
        static volatile bool finished = false;
        static Stopwatch watch;
        static Solver solver;

        static void Main(string[] args)
        {
            unsafe {
                Console.WriteLine($"move: {sizeof(Move)}, hashstate: {sizeof(HashState)} ptr: {sizeof(IntPtr)}");
            }

            var level = Level.Parse(File.ReadAllText(args[0]));
            solver = new Solver(level);
            

            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("");
                Console.Write(" {0:h\\:mm\\:ss\\.f} AVG Rates: {1:0} / {2:0}; {3:0} / {4:0}                     ", 
                    watch.Elapsed,
                    PerformanceCounter.Counters["fwd_working_set"].Average,
                    PerformanceCounter.Counters["fwd_visited_set"].Average,
                    PerformanceCounter.Counters["bck_working_set"].Average,
                    PerformanceCounter.Counters["bck_visited_set"].Average
                );
            };


            watch = new Stopwatch();
            watch.Start();

            try {
                Task.WhenAny([
                    solver.Solve().ContinueWith((_) => { finished = true; }),
                    PrintStats()
                ]).Wait();
            } catch (AggregateException ae) {
                foreach (var e in ae.InnerExceptions) {
                    Console.WriteLine(e.Message);
                }
            }

            watch.Stop();
            Console.WriteLine("Time: " + watch.Elapsed);

            solver.PrintSolution();
        }

        private static async Task PrintStats()
        {
            PerformanceCounter.Register("fwd_working_set", () => solver.statesToProcess.Count);
            PerformanceCounter.Register("bck_working_set", () => solver.statesToProcessBck.Count);
            PerformanceCounter.Register("fwd_visited_set", () => solver.forwardVisitedStates.Count);
            PerformanceCounter.Register("bck_visited_set", () => solver.backwardVisitedStates.Count);


            while (!finished)
            {
                await Task.Delay(500);

                var elapsed = watch.Elapsed.TotalSeconds;

                Console.Write("\r {0:h\\:mm\\:ss\\.f} Mem/Accu: {1} / {2} MB, GC: {3}/{4}/{5} States: {6:n0} / {7:n0}; {8:n0} / {9:n0} Rates: {10:0} / {11:0}; {12:0} / {13:0}                     ", /* BranchF: {6:0.00} */
                    watch.Elapsed,
                    Process.GetCurrentProcess().WorkingSet64 >> 20,
                    GC.GetTotalAllocatedBytes() >> 20,
                    GC.CollectionCount(0),
                    GC.CollectionCount(1),
                    GC.CollectionCount(2),
                    solver.statesToProcess.Count,
                    solver.forwardVisitedStates.Count,
                    solver.statesToProcessBck.Count,
                    solver.backwardVisitedStates.Count,
                    PerformanceCounter.Counters["fwd_working_set"].Tick(elapsed),
                    PerformanceCounter.Counters["fwd_visited_set"].Tick(elapsed),
                    PerformanceCounter.Counters["bck_working_set"].Tick(elapsed),
                    PerformanceCounter.Counters["bck_visited_set"].Tick(elapsed)
                    // solver.movesFwd.Count, solver.movesFwd.items.Length,
                    // solver.movesBck.Count, solver.movesBck.items.Length, 

                // solver.statesToProcessBck.GetTop3Count()
                );
                if (Process.GetCurrentProcess().WorkingSet64 > (8L << 30))
                {    // 8GB
                    throw new OutOfMemoryException();
                }
            }
            Console.WriteLine();
        }
    }
}
