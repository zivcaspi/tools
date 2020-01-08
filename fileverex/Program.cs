using System;
using System.IO;
using System.Text;

class Program
{
    private static string[] s_properties =
    {
        // The following list is taken from nt\sdktools\filever\filever.h:
        "CompanyName",
        "FileDescription",
        "InternalName",
        "OriginalFilename",
        "ProductName",
        "ProductVersion",
        "FileVersion",
        "LegalCopyright",
        "LegalTrademarks",
        "PrivateBuild",
        "SpecialBuild",
        "Comments",
        "Applies To",
        "Build Date",
        "Installation Type",
        "Installer Engine",
        "Installer Version",
        "KB Article Number",
        "Package Type",
        "Proc. Architecture",
        "Self-Extractor Version",
        "Support Link",

        // The following list is Echoes-specific:
        // "Microsoft.Cloud.Platform.Utils.CodeType",
        // "Microsoft.Cloud.Platform.EventsKit.Defined",
    };

    public static int Main(string[] args)
    {
        // TODO:
        // 1. Dump the file version info
        // 2. Try to load the file as a .NET assembly, and dump *that* info if successful
        // 3. Check the PE header, and write the bitness if you can find it
        // 4. Argument parsing
        var file = args[0];

        using (ExtendedFileVersionInfo info = ExtendedFileVersionInfo.TryCreate(file))
        {
            Log.Info("File " + file + " has the following attributes:");
            StringBuilder sb = new StringBuilder();
            foreach (string name in s_properties)
            {
                try
                {
                    if (info.TryGetVersionString(name, out var value))
                    {
                        Log.Info("  " + name + " = " + value);
                    }
                }
                catch (ExtendedFileVersionInfoException)
                {
                    sb.Append("  " + name + Environment.NewLine);
                }
            }
            string notFound = sb.ToString();
            if (notFound != "")
            {
                Log.Verbose(Environment.NewLine + "The following values are not found in the version resource:" + Environment.NewLine + notFound);
            }
        }
        return 0;
    }
}

public static class Log
{
    public static void Info(string text)
    {
        Console.WriteLine(text);
    }

    public static void Verbose(string text)
    {
        var currentColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = currentColor;
        }
    }
}