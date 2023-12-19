using System;
using System.IO;
using System.Text;

static class Program
{
    private static StringBuilder sb = new StringBuilder();
    private static string pattern = null;

    public static int Main(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
        {
            return Usage();
        }

        if (!File.Exists(args[0]))
        {
            Console.WriteLine("File does not exist.");
            return 1;
        }

        if (Console.IsOutputRedirected)
        {
            Console.WriteLine("Input is redirected, why use page?");
            return 2;
        }

        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
        {
            pattern = args[1];
        }

        var nLines = Math.Max(Console.WindowHeight, 4);
        var width = Math.Max(Console.WindowWidth, 4);
        using (var file = File.OpenRead(args[0]))
        {
            using (var reader = new StreamReader(file, detectEncodingFromByteOrderMarks: true, bufferSize: 4096))
            {
                if (!string.IsNullOrEmpty(pattern))
                {
                    if (!ReadUntilPattern(reader, pattern))
                    {
                        Console.WriteLine(">>> Pattern not found <<<");
                        return 0;
                    }
                }

                var done = false;
                while (!done)
                {
                    for (int i = 0; i < nLines; i++)
                    {
                        var line = ReadLine(reader, width);
                        if (line == null)
                        {
                            done = true;
                            break;
                        }
                        Console.WriteLine(line);
                    }

                    if (!done)
                    {
                        // TODO: Just wait for input... In the future we'll remove this line and continue again
                        Console.WriteLine();
                        Console.WriteLine(">>> Press ENTER to continue <<<");
                        Console.ReadLine();
                    }
                }
            }
        }
        return 0;
    }

    private static bool ReadUntilPattern(TextReader reader, string pattern)
    {
        var pos = FindSubstringInStream.IndexOf(reader.Read, pattern);
        return pos != -1;
    }

    public static int Usage()
    {
        Console.WriteLine("Usage: page <filename> [<pattern>]");
        return 0;
    }

    private static string? ReadLine(TextReader reader, int width)
    {
        sb.Clear();
        for (int i = 0; i < width; i++)
        {
            var ch = reader.Read();
            if ( ch <= 0)
            {
                return null;
            }

            if (ch == '\r' || ch == '\n')
            {
                break;
            }
            sb.Append((char)ch);
        }
        return sb.ToString();
    }
}


