﻿using System.Threading.Tasks;
using System.IO;
using System;
using System.Diagnostics;

namespace soko
{
    class Program
    {
        static void Main(string[] args)
        {
            var level = Level.Parse(File.ReadAllText(args[0]));
            Task.WhenAll(new [] { RunSolve(level, false), RunSolve(level, true) }).Wait();
            PrintStatistics();
        }

        private static Task RunSolve(Level level, bool reversed) {
            return Task.Run(() => {
                var solver = new Solver(level);
                if (solver.Solve(reversed)) solver.PrintSolution();
            });
        }

        private static void PrintStatistics() 
        {
            Console.WriteLine($"Private Mem: {Process.GetCurrentProcess().PrivateMemorySize64/(1<<20)} MB");
            Console.WriteLine($"GC Mem: {GC.GetTotalMemory(false)/(1<<20)} MB");
            Console.WriteLine($"GC Allocated: {GC.GetTotalAllocatedBytes()/(1<<20)} MB");
            Console.WriteLine($"GC Gen0 Coll: {GC.CollectionCount(0)}");
            Console.WriteLine($"GC Gen1 Coll: {GC.CollectionCount(1)}");
            Console.WriteLine($"GC Gen2 Coll: {GC.CollectionCount(2)}");
        }

    }
}
