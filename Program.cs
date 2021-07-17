using System.Threading.Tasks;
using System.IO;
using System;
using System.Diagnostics;

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
            
            watch = new Stopwatch();
            watch.Start();

            Task.WhenAll(new [] {
                solver.Solve().ContinueWith((_) => { finished = true; }),
                PrintStats()
            }).Wait();

            watch.Stop();
            Console.WriteLine("Time: " + watch.Elapsed);

            solver.PrintSolution();
        }

        private static async Task PrintStats() 
        {
            while (!finished) {
                await Task.Delay(500);
                Console.Write("\r {0:h\\:mm\\:ss\\.f} Mem/Alloc: {1} / {2} MB, GC: {3}/{4}/{5} States: {6} / {7} [coll: {8} %]                     ", /* BranchF: {6:0.00} */
                    watch.Elapsed,
                    Process.GetCurrentProcess().PrivateMemorySize64/(1<<20),
                    GC.GetTotalAllocatedBytes()/(1<<20),
                    GC.CollectionCount(0),
                    GC.CollectionCount(1),
                    GC.CollectionCount(2),
                    solver.statesToProcess.Count,
                    solver.forwardVisitedStates.Count,
                    solver.forwardVisitedStates.collisions * 100 / solver.forwardVisitedStates.Count);
            }
            Console.WriteLine();
        }
    }
}
