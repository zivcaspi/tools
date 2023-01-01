using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

[MemoryDiagnoser(displayGenColumns: false)]
[HideColumns("Error", "StdDev", "Median", "RatioSD")]
public class Program
{
    public static void Main(string[] args)
    {
        if (args == null || args.Length == 0)
        {
        var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
        var summary =
#if DEBUG
            switcher.RunAllJoined(new DebugInProcessConfig());
#else
            switcher.RunAllJoined();
#endif

        }
        else
        {
            // Use this to run:
            //   dotnet run -c Release -f net6.0 --filter ** --runtimes net6.0 net7.0
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }

        Console.WriteLine("Done");
        Console.ReadLine();
    }

    private string m_a;
    private string m_b;

    [GlobalSetup]
    public void Setup()
    {
        m_a = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        m_b = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    }

    [Benchmark(Baseline=true)]
    public string Baseline() => m_a + m_b;

    [Benchmark]
    public string Splice() => $"{m_a}{m_b}";
}

