// A debugging program that echoes the piped input (if any) and each of its arguments (if any)
using System;

public static class Program
{
    public static int Main(string[] args)
    {
        if (Console.IsInputRedirected)
        {
            WriteRule("Redirected input:");
            while (true)
            {
                var line = Console.In.ReadLine();
                if  (line == null)
                {
                    break;
                }
                Console.WriteLine(line);
            }
        }
        else
        {
            WriteRule("(Input is not redirected)");
        }


        if (args != null && args.Length > 0)
        {

            for (int i = 0; i < args.Length; i++)
            {
                WriteRule($"arg[{i}]:");

                Console.WriteLine(args[i]);
            }
        }
        else
        {
            WriteRule("(No command-line arguments)");
        }

        WriteRule("(Done)");

        return 0;
    }

    private static void WriteRule(string text = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLine(new string('-', 80));
            return;
        }

        if (text.Length > 70)
        {
            text = text.Substring(0, 70);
        }

        Console.Write(new string('-', 80 - text.Length - 3));
        Console.Write(' ');
        Console.Write(text);
        Console.WriteLine(" -");
    }
}