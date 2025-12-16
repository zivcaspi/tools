using System;
using System.Diagnostics;

class Program
{
    // This utility measures how long it takes to execution the provided command.
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: ztime <command> [args...]");
            return;
        }

        Console.WriteLine($"ztime: Starting...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = args[0],
                Arguments = string.Join(" ", args, 1, args.Length - 1), // TODO: Escape arguments
                UseShellExecute = false
            }).WaitForExit();
        }
        finally
        {
            Console.WriteLine($"ztime: Elapsed time: {sw.Elapsed}");
        }
    }
}


