using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace as_unc
{
    class Program
    {
        #region Constants and data
        const string c_badOption =
@"  Bad option '{0}'. Use option '-?' to get help.";

        const string c_usage =
        @"

  Name:

    as-unc.exe

  Description:

    Converts a URL string into a UNC string, if possible.

  Usage:

    as-unc.exe -h | -?
    as-unc.exe <Url> [<processname>]
    as-unc.exe -open <Url>
    as-unc.exe <SourceDepotPath> (TODO: Add support for this)

  Examples:

    as-unc c:/unix/name/of/file notepad2.exe

";

        string m_url;
        bool m_open;
        string m_process;
        #endregion

        #region Main and construction
        static int Main(string[] args)
        {
            Program program = new Program(args);
            return program.Run();
        }

        Program(string[] args)
        {
            if (!TryParseArgs(args))
            {
                System.Environment.Exit(1);
            }
        }
        #endregion

        #region Implementation
        bool TryParseArgs(string[] args)
        {
            // Command-line args
            if (args == null || args.Length < 1)
            {
                Console.WriteLine(c_usage);
                return false;
            }

            for (int a = 0; a < args.Length; a++)
            {
                string arg = args[a];
                if (arg != null && arg.Length > 0)
                {
                    char c = arg[0];
                    if (c == '-' || c == '/')
                    {
                        if (arg == "-open" || arg == "/open")
                        {
                            m_open = true;
                            continue;
                        }

                        // The argument is an option.
                        if (arg.Length < 2)
                        {
                            Console.WriteLine(c_badOption, arg);
                            Console.WriteLine("Bad option '{0}'. Use option '-?' to get help.", arg);
                            return false;
                        }

                        char d = arg[1];
                        if (d == '?' || d == 'h')
                        {
                            Console.WriteLine(c_usage);
                            // No need to get other options.
                            return false;
                        }
                        else if (d == '/')
                        {
                            // This is actually an SD path (//depot-or-client/...)
                            m_url = arg;
                        }
                    }
                    else
                    {
                        // The argument is not an option, so it must be the URL.
                        if (m_url == null)
                        {
                            m_url = arg;
                        }
                        else if (m_process == null)
                        {
                            m_process = arg;
                        }
                    }
                }
            }

            if (m_url == null)
            {
                Console.WriteLine(c_usage);
                return false;
            }

            return true;
        }

        int Run()
        {
            string name = this.m_url;
            Uri url = null;
            string file = null;
            if (name.StartsWith("//"))
            {
                // Name is a Source Depot path.
                // Shell to 'sd.exe where' on 'name' to get the local path.
                // Get rid of the first '//xxx ' (basically, everything up to the second '//').
                // Then, we have two strings on the line: '//XXX/YYY Z:\AAA\BBB\YYY'.
                // The answer we seek is the Z:\ part, which we find by locating 'Z:'.
                // Caveats: We might get back more than one line if there are warnings (such as backup issues).
                //          We might also get an error "Path...".
                // To work around these, we only parse the first line that starts with a '//'.
                
                // TODO: The algorithm above was not implemented yet...
                Console.WriteLine("TODO: Add support for SD paths (//depot/... and similar)");
                return 2;
            }

            if (name.StartsWith("file:")
                || name.StartsWith("\\")
                || name.StartsWith("http:")
                || name.StartsWith("https:"))
            {
                url = new Uri(name);
            }

            if (url != null)
            {
                // url must be non-null at this point.
                if (url.IsFile || url.IsUnc)
                {
                    file = url.LocalPath;
                }
            }

            if (file != null)
            {
                return Success(file, m_open, m_process);
            }

            try
            {
                var fi = new FileInfo(name);
                if (fi != null)
                {
                    return Success(fi.FullName, m_open, m_process);
                }
            }
            catch { }

            // Failed.
            System.Console.WriteLine(url);
            string[] segments = url.Segments;
            foreach (string segment in segments)
            {
                System.Console.WriteLine("  Segment: " + segment);
            }
            
            string query = Uri.UnescapeDataString(url.Query);
            if (!string.IsNullOrEmpty(query))
            {
                System.Console.WriteLine("  Query: " + query);
            }

            string fragment = Uri.UnescapeDataString(url.Fragment);
            if (!string.IsNullOrEmpty(fragment))
            {
                System.Console.WriteLine("  Fragment: " + fragment);
            }

            return 1;
        }

        static int Success(string u, bool o, string p)
        {
            if (p != null && !o)
            {
                // TODO: Check if the file exists etc.
                System.Console.WriteLine($"{p} {u}");
                System.Diagnostics.Process.Start(p, u);
            }
            else
            {
                System.Console.WriteLine(u);
            }

            if (o)
            {
                var psi = new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = u,
                    UseShellExecute = true,
                };
                System.Diagnostics.Process.Start(psi);
            }

            return 0;
        }
        #endregion
    }
}
