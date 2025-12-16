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
            var psi = new ProcessStartInfo
            {
                FileName = args[0],
                UseShellExecute = false
            };
            for (int i = 1; i < args.Length; i++)
            {
                psi.ArgumentList.Add(args[i]);
            }
            Process.Start(psi).WaitForExit();
        }
        finally
        {
            Console.WriteLine($"ztime: Elapsed time: {sw.Elapsed}");
        }
    }
}


