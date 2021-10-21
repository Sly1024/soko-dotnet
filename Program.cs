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

            // PlayerReachable pr = new PlayerReachable();
            // pr.GenerateMoveTable();

            var level = Level.Parse(File.ReadAllText(args[0]));
            solver = new Solver(level);
            
            watch = new Stopwatch();
            watch.Start();

            Task.WhenAny(new [] {
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
                Console.Write("\r {0:h\\:mm\\:ss\\.f} Mem/Alloc: {1} / {2} MB, GC: {3}/{4}/{5} States: {6:n0} / {7:n0}; {8:n0} / {9:n0}                       ", /* BranchF: {6:0.00} */
                    watch.Elapsed,
                    Process.GetCurrentProcess().PrivateMemorySize64 >> 20,
                    GC.GetTotalAllocatedBytes() >> 20,
                    GC.CollectionCount(0),
                    GC.CollectionCount(1),
                    GC.CollectionCount(2),
                    solver.statesToProcess.Count,
                    solver.forwardVisitedStates.Count,
                    solver.statesToProcessBck.Count,
                    solver.backwardVisitedStates.Count
                    // solver.statesToProcessBck.GetTop3Count()
                    );
                if (Process.GetCurrentProcess().PrivateMemorySize64 > (4096L << 20)) {
                    throw new OutOfMemoryException();
                }
            }
            Console.WriteLine();
        }
    }
}
