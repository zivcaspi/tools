using System;
using System.Net;

using Kusto.Cloud.Platform.Data;
using Kusto.Cloud.Platform.Utils;

using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;

public class Program
{
    static void Main(string[] args)
    {
        // Library.Initialize(CloudPlatformExecutionMode.Library, ClientServerProfile.Client);

        Console.WriteLine("DefaultConnectionLimit={0}", ServicePointManager.DefaultConnectionLimit);

        var kcsb = new KustoConnectionStringBuilder("https://help.kusto.windows.net/Samples").WithAadUserPromptAuthentication();
        using (var client = KustoClientFactory.CreateCslQueryProvider(kcsb))
        {
            client.ExecuteQuery("print 123").Consume();

            var sp = ServicePointManager.FindServicePoint("https://help.kusto.windows.net/", null);
            Console.WriteLine("DefaultConnectionLimit={0}", ServicePointManager.DefaultConnectionLimit);
            Console.WriteLine("sp.ConnectionLimit={0}", sp.ConnectionLimit);
        }

        Console.ReadLine();
    }
}
