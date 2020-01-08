using System;
using System.IO;
using System.Text;

class Program
{
    private string file;

    private string[] properties =
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
        "Microsoft.Cloud.Platform.Utils.CodeType",
        "Microsoft.Cloud.Platform.EventsKit.Defined",
    };

    public static int Main(string args[])
    {
        // TODO:
        // 1. Dump the file version info
        // 2. Try to load the file as a .NET assembly, and dump *that* info if successful
        // 3. Check the PE header, and write the bitness if you can find it
        using (ExtendedFileVersionInfo info = new ExtendedFileVersionInfo(file))
        {
            Log.Info("File " + file + " has the following attributes:");
            StringBuilder sb = new StringBuilder();
            foreach (string name in properties)
            {
                try
                {
                    string value = info[name];
                    Log.Info("  " + name + " = " + value);
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
    }
}
