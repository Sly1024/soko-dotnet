using System.IO;
using System;

namespace soko
{
    class Program
    {
        static void Main(string[] args)
        {
            var level = Level.Parse(File.ReadAllText(args[0]));
            var solver = new Solver(level);
            solver.solve();
        }
    }
}
