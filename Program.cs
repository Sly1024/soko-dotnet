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

        static void Main(string[] args)
        {
            var level = Level.Parse(File.ReadAllText(args[0]));
            var solver = new Solver(level);
            
            watch = new Stopwatch();
            watch.Start();

            Task.WhenAll(new [] {
                solver.Solve().ContinueWith((_) => { finished = true; }),
                PrintStats()
            }).Wait();

            watch.Stop();
            // Console.WriteLine("Time: " + watch.Elapsed);

            solver.PrintSolution();
        }

        private static async Task PrintStats() 
        {
            while (!finished) {
                await Task.Delay(500);
                Console.Write("\r {5:h\\:mm\\:ss\\.f} Mem: {0} MB,  Alloc: {1} MB, GC: {2}/{3}/{4}",
                    Process.GetCurrentProcess().PrivateMemorySize64/(1<<20),
                    GC.GetTotalAllocatedBytes()/(1<<20),
                    GC.CollectionCount(0),
                    GC.CollectionCount(1),
                    GC.CollectionCount(2),
                    watch.Elapsed);
            }
            Console.WriteLine();
        }
    }
}
