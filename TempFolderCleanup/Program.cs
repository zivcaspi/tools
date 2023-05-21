using System;
using System.IO;

public static class C
{
    public static int Main(string[] args)
    {
        // This program goes over all the top-level directories in arg[0]
        // (or the TEMP folder if none is specified), and remove those that
        // are empty.
        var root = args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : Path.GetTempPath()!;

        Console.WriteLine("Cleaning all top-level empty directories under '{0}'", root);
        Console.WriteLine("Hit 'y' to continue, any other key to stop");
        if (Console.ReadLine() != "y")
        {
            Console.WriteLine("Quitting.");
            return 1;
        }

        Console.WriteLine("Deleting...");
        var enumerationOptions = new EnumerationOptions
        {
            AttributesToSkip = 0,
            IgnoreInaccessible = false,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        var skipped = 0;
        var empty = 0;

        foreach (var folder in Directory.GetDirectories(root))
        {
            var files = Directory.GetFiles(folder, "*", enumerationOptions);
            if (files != null && files.LongLength > 0)
            {
                // Skip this directory
                skipped++;
                continue;
            }

            var directories = Directory.GetDirectories(folder, "*", enumerationOptions);
            if (directories != null && directories.LongLength > 0)
            {
                // This this directory
                skipped++;
                continue;
            }

            Console.WriteLine("Directory {0} is empty, can be removed", folder);
            empty++;

            try
            {
                Directory.Delete(folder);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to delete directory {0}");
                Console.WriteLine(ex.ToString());
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debugger.Break();
                }
            }
        }

        Console.WriteLine("In total, {0} are empty and were removed, {1} are not empty and were skipped", empty, skipped);
        return 0;
    }
}

