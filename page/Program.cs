using System;
using System.IO;
using System.Text;

class Program
{
    public static int Main(string[] args)
    {
        Program program = new Program();
        return program.Run(args);
    }

    private StringBuilder m_sb = new StringBuilder();
    private string m_pattern = null;

    public Program()
    {
    }

    public int Run(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
        {
            return Usage();
        }

        if (args[0] == "diff")
        {
            return Diff(args);
        }
        else
        {
            return Page(args);
        }
    }

    public int Diff(string[] args)
    {
        if (args.Length < 3)
        {
            return Usage();
        }

        var lhs = args[1];
        CheckFileExists(lhs);

        var rhs = args[2];
        CheckFileExists(rhs);

        (int nLines, int width) = GetConsoleWindow();

        using var leftFile = File.OpenRead(lhs);
        using var rightFile = File.OpenRead(rhs);

        using var leftReader = new StreamReader(leftFile, detectEncodingFromByteOrderMarks: true, bufferSize: 4096);
        using var rightReader = new StreamReader(rightFile, detectEncodingFromByteOrderMarks: true, bufferSize: 4096);

        var done = false;
        while (!done)
        {
            var leftLine = ReadLine(leftReader, width);
            var rightLine = ReadLine(rightReader, width);
            if (leftLine == null && rightLine == null)
            {
                done = true;
                break;
            }
            if (leftLine == rightLine)
            {
                continue;
            }
            Console.WriteLine("Diff found!");
            Console.WriteLine(leftLine);
            Console.WriteLine(rightLine);
            return 1;
        }

        return 0;
    }

    public int Page(string[] args)
    {
        var lhs = args[0];
        CheckFileExists(lhs);

        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
        {
            m_pattern = args[1];
        }

        (int nLines, int width) = GetConsoleWindow();

        using (var file = File.OpenRead(lhs))
        {
            using (var reader = new StreamReader(file, detectEncodingFromByteOrderMarks: true, bufferSize: 4096))
            {
                if (!string.IsNullOrEmpty(m_pattern))
                {
                    if (!ReadUntilPattern(reader, m_pattern))
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

    private unsafe static bool ReadUntilPattern(TextReader reader, string pattern)
    {
        long pos;

        // An optimization for single-character patterns
        // TODO: In this case how does the caller know which position we are in the buffer? We need to tell it that! Currently it'll simply start showing N characters _after_ the change (depending on the sizeo f the buffer after the pattern)
        if (pattern.Length == 1)
        {
            var ch = pattern[0];
            var buffer = new char[4096];
            fixed (char* p = buffer)
            {
                while (true)
                {
                    var nCharacters = reader.ReadBlock(buffer, 0, buffer.Length);
                    if (nCharacters == 0)
                    {
                        return false;
                    }

                    for (var q = p; q < p + Math.Min(nCharacters, buffer.Length); q++)
                    {
                        if (*q == ch)
                        {
                            return true;
                        }
                    }
   
                }
            }
        }

        pos = FindSubstringInStream.IndexOf(reader.Read, pattern);
        return pos != -1;
    }

    private static int Usage()
    {
        Console.WriteLine("page - a small utility to page through large files");
        Console.WriteLine();
        Console.WriteLine("  page <filename> [<pattern>]");
        Console.WriteLine();
        Console.WriteLine("    Page through file <filename>, starting right after the substring <pattern> (or position 0 if unspecified).");
        Console.WriteLine();
        Console.WriteLine("  page diff <filename1> <filename2>");
        Console.WriteLine();
        Console.WriteLine("    Page through the diff of the two files.");
        return 0;
    }

    private string? ReadLine(TextReader reader, int width)
    {
        m_sb.Clear();
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
            m_sb.Append((char)ch);
        }
        return m_sb.ToString();
    }

    private void CheckFileExists(string path)
    {
        if (path == null || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("File name cannot be empty"); // TODO: Really this should be to stderr and red
            Environment.Exit(4);
        }

        if (!File.Exists(path))
        {
            Console.WriteLine("File does not exist: '{0}'", path);
            Environment.Exit(5);
        }
    }

    private (int nLines, int width) GetConsoleWindow()
    {
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine("Input is redirected, why use page?"); // TODO: Red and to stderr
            Environment.Exit(2);
        }

        var nLines = Math.Max(Console.WindowHeight, 4);
        var width = Math.Max(Console.WindowWidth, 4);
        return (nLines, width);
    }
}


