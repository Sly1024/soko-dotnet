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
            var level = Level.Parse(File.ReadAllText(args[0]));
            solver = new Solver(level);

            unsafe
            {
                Console.WriteLine($"move: {sizeof(Move)}, hashstate: {sizeof(HashState)}, ptr: {sizeof(IntPtr)}, ToProc: {sizeof(ToProcess)}|{sizeof(ToProcessBck)} threads: 2x{Solver.NumSolverThreadsPerSide}");
            }

            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("");
                Console.Write(" {0:h\\:mm\\:ss\\.f} AVG Rates: {1:0} / {2:0}; {3:0} + {4:0} = {5:0}                     ",
                    watch.Elapsed,
                    PerformanceCounter.Counters["fwd_working_set"].Average,
                    PerformanceCounter.Counters["bck_working_set"].Average,
                    PerformanceCounter.Counters["fwd_visited_set"].Average,
                    PerformanceCounter.Counters["bck_visited_set"].Average,
                    PerformanceCounter.Counters["fwd_visited_set"].Average+
                    PerformanceCounter.Counters["bck_visited_set"].Average
                );
            };


            watch = new Stopwatch();
            watch.Start();

            try
            {
                Task.WhenAny([
                    solver.Solve().ContinueWith((_) => { finished = true; }),
                    PrintStats()
                ]).Wait();
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    Console.WriteLine(e.Message);
                }
            }

            watch.Stop();
            PrintCurrentStats();
            Console.WriteLine();
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
                PrintCurrentStats();

                if (Process.GetCurrentProcess().WorkingSet64 > (8L << 30))
                {    // 8GB
                    throw new OutOfMemoryException();
                }
            }
            Console.WriteLine();
        }

        private static readonly string[] ThousandMark = ["", "K", "M", "B", "T", "Q"];
        private static string Num(double n)
        {
            char sign = n < 0 ? '-' : ' ';
            n = Math.Abs(n);

            int thousands = 0;
            while (n > 1000)
            {
                n *= 0.001;
                thousands++;
            }
            var result = string.Format("{0}{1:0.0}{2}", sign, n, ThousandMark[thousands]);
            while (result.Length < 7) result = " " + result;
            return result;
        }

        private static void PrintCurrentStats()
        {
            var elapsed = watch.Elapsed.TotalSeconds;
            // Console.Write("\r {0:h\\:mm\\:ss\\.f} Mem/Accu: {1} / {2} MB, GC: {3}/{4}/{5} AStates: {6}|{7} - Visited: {8}|{9} - ARates: {10}|{11} - VRates: {12}|{13}                     ", /* BranchF: {6:0.00} */

            Console.Write("\r {0:h\\:mm\\:ss\\.f} Mem/Accu: {1} / {2} MB, GC: {3}/{4}/{5} Active: {6}|{7} ({10}|{11}/s) - Visited: {8}|{9} ({12}|{13}/s)  LP: {14}|{15}                    ", /* BranchF: {6:0.00} */
                watch.Elapsed,
                Process.GetCurrentProcess().WorkingSet64 >> 20,
                GC.GetTotalAllocatedBytes() >> 20,
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2),
                // States
                Num(solver.statesToProcess.Count),
                Num(solver.statesToProcessBck.Count),
                Num(solver.forwardVisitedStates.Count),
                Num(solver.backwardVisitedStates.Count),
                // Rates
                Num(PerformanceCounter.Counters["fwd_working_set"].Tick(elapsed)),
                Num(PerformanceCounter.Counters["bck_working_set"].Tick(elapsed)),
                Num(PerformanceCounter.Counters["fwd_visited_set"].Tick(elapsed)),
                Num(PerformanceCounter.Counters["bck_visited_set"].Tick(elapsed)),
                solver.statesToProcess.lpd_counter,
                solver.statesToProcessBck.lpd_counter
            // solver.movesBck.Count, solver.movesBck.items.Length, 

            // solver.statesToProcessBck.GetTop3Count()
            );
        }
    }
}
