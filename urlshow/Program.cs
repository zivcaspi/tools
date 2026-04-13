using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web;

public class Program
{
    public static int Main(string[] args)
    {
        if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            args = new string[]
            {
                //"msit.powerbi.com/groups/me/queryworkbenches/querydeeplink?experience=fabric-developer&cluster=https://kuskushead.westeurope.kusto.windows.net/&database=Kuskus&query=H4sIAAAAAAAAAwXBwQpAQBQF0L3yD3c5UzZ%2BQNmzkb0e3RAzo3mDyMc7p94Zk%2BLDvTAS%2FeqoSdyBCjIHUy42zz7o6ZzE9SWmcPpkbIEkGwfxj2mpKjMtxgcNL%2B4FOooG%2FwNrkFmxWwAAAA%3D%3D",
                //"https://app.fabric.microsoft.com/groups/me/queryworkbenches/querydeeplink?cluster=kuskushead.westeurope.kusto.windows.net&database=Kuskus&query=H4sIAAAAAAAEAAXBsQqDMBQF0D2Qf7hjAi7+QMHdLtJdXsvFhJqk5EVF8eN7zrCyNsWNI7ASr5ioTdIPD8hSXB+8NTd0S0lqvIhP2XJzvkOTL2fJp3tSVRZ6vE+M3Ll2mChasjXW/AGxuOlNXwAAAA==",
                "https://help.kusto.windows.net:443/v1/rest/auth/metadata?a+b+c=d+e+f&g%2fh%2fi=%3d%3d%3d&j%25k%25l#frag"
            };
        }

        foreach (var arg in args)
        {
            string url = arg;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            var uri = new Uri(url);

            Console.WriteLine();
            Console.WriteLine($"URL={url}");
            Console.WriteLine($"Host={uri.IdnHost}");
            Console.WriteLine($"Path={uri.AbsolutePath}");

            var queryProperties = HttpUtility.ParseQueryString(uri.Query);
            if (queryProperties != null && queryProperties.Count > 0)
            {
                for (int i = 0; i < queryProperties.Count; i++)
                {
                    var key = queryProperties.GetKey(i);
                    var values = queryProperties.GetValues(i);
                    if (values == null || values.Length == 0)
                    {
                        Console.WriteLine($"  {key} (no value)");
                    }
                    else if (values.Length == 1)
                    {
                        Console.WriteLine($"  {key}={DecodeValue(values[0])}");
                    }
                    else
                    {
                        Console.WriteLine($"  {key}=");
                        foreach (var value in values)
                        {
                            Console.WriteLine($"    {DecodeValue(value)}");
                        }
                    }
                }
            }
        }

        return 0;
    }

    private static string DecodeValue(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            var compressed = new MemoryStream(bytes);
            using var gz = new GZipStream(compressed, CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            gz.CopyTo(decompressed);

            decompressed.Position = 0;
            return Encoding.UTF8.GetString(decompressed.ToArray());      
        }
        catch
        {
            return value;
        }
    }
}