using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace zget
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Program program = new Program(args);
            program.Run();
            // For testing purposes: program.Run(); program.Run(); program.Run();

            if (Debugger.IsAttached)
            {
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
        }

        static string usage =
@"

  Name:

    zget.exe

  Description:

    Retrieves a web resource to a local file.

  Usage:

    zget.exe -h | -?
    zget.exe (<srcUrl> | -c) [-o <dstUrl>] [-p <proxyServer>] [-v] [-vv] [-s] [-e] [-w] [-r] [-i] [-b <bearerToken>]

  Remarks:

    If a <dstUrl> is not specified, the resource will be stored in the current
    directory using a unique name derived from <srcUrl>. If a <dstUrl> is specified
    as 'CON', 'CON:', or 'console', we output to the console instead.

    Use -v to request progress updated to be printed to the console.
    Setting the environment variable ZGET_PROGRESS also achieves this goal.

    Use -vv to request the program is very verbose in reporting stuff
    to the console.
    Setting the environment variable ZGET_ VERYVERBOSE also achieves this goal.

    Use -p to connect via a specified proxy server.
    Setting the environment variable ZGET_PROXY also achieves this goal.

    Use -s to 'start' the file once it is downloaded automatically.

    Use -e to download only files that do not already exist locally.

    Use -w to indicate web authentication is needed (for example, for SPO).

    Use -r to disable automatically following redirects.

    Use -i to ignore SSL cert errors (or send HTTPS to an IP address).

    Use -b to specify the bearer token to include in the request.

    Use -h or -? to get this usage screen.

";

        static string badOption =
@"  Bad option '{0}'. Use option '-?' to get help.";
        static string badClipboard =
@"  Clipboard does not hold a URL.";

        string srcUrl;
        string dstUrl;
        bool autoDstUrl = true; // If true, indicates that the user wants us to calculate it ourselves
        string proxyServer;
        string bearerToken;
        bool verbose;
        bool veryverbose;
        bool autostart;
        bool onlyIfNotExist;
        bool webAuthentication;
        bool noRedirect;
        bool ignoreSslErrors;

        Program(string[] args)
        {
            if (!TryParseArgs(args))
            {
                System.Environment.Exit(1);
            }
        }

        bool TryParseArgs(string[] args)
        {
            // Environment variables.
            // (These are trumped by command-line args, so they get to be first.)
            if (System.Environment.GetEnvironmentVariable("ZGET_PROGRESS") != null)
            {
                verbose = true;
            }
            if (System.Environment.GetEnvironmentVariable("ZGET_VERYVERBOSE") != null)
            {
                verbose = true;
                veryverbose = true;
            }
            string proxy = System.Environment.GetEnvironmentVariable("ZGET_PROXY");
            if (proxy != null)
            {
                proxyServer = proxy;
            }
            string bearer = System.Environment.GetEnvironmentVariable("ZGET_BEARER_TOKEN");
            if (bearer != null)
            {
                bearerToken = bearer;
            }

            // Command-line args
            if (args == null || args.Length < 1)
            {
                Console.WriteLine(usage);
                return false;
            }

            bool unquotedSrcUrl = false;

            for (int a = 0; a < args.Length; a++)
            {
                string arg = args[a];
                if (arg != null && arg.Length > 0)
                {
                    char c = arg[0];
                    if (c == '-' || c == '/')
                    {
                        // The argument is an option.
                        if (arg.Length < 2)
                        {
                            Console.WriteLine(badOption, arg);
                            Console.WriteLine("Bad option '{0}'. Use option '-?' to get help.", arg);
                            return false;
                        }

                        char option = arg[1];
                        if (option == '?' || option == 'h')
                        {
                            Console.WriteLine(usage);
                            // No need to get other options.
                            return false;
                        }
                        else if (option == 'v')
                        {
                            verbose = true;
                            if (arg.Length > 2 && arg[2] == 'v')
                            {
                                veryverbose = true;
                            }
                        }
                        else if (option == 'o')
                        { // TODO: Write a function to parse options such as -o and -p
                            a++;
                            if (a >= args.Length)
                            {
                                Console.WriteLine(badOption, arg);
                                return false;
                            }
                            else
                            {
                                // TODO: Ensure that dstUrl does not start with a '-' or '/'...
                                dstUrl = args[a];
                                autoDstUrl = false;
                            }
                        }
                        else if (option == 'p')
                        {
                            a++;
                            if (a >= args.Length)
                            {
                                Console.WriteLine(badOption, arg);
                                return false;
                            }
                            else
                            {
                                // TODO: Ensure that proxyServer does not start with a '-' or '/'...
                                proxyServer = args[a];
                            }
                        }
                        else if (option == 's')
                        {
                            autostart = true;
                        }
                        else if (option == 'c')
                        {
                            /*
                            object data = System.Windows.Clipboard.GetData(DataFormats.StringFormat);
                            srcUrl = data as string;
                            if (string.IsNullOrEmpty(srcUrl))
                            {
                                Console.WriteLine(badClipboard);
                                return false;
                            }
                            */
                            Console.WriteLine("Copying data from the clipboard is currently not supported, until Ziv adds this support (back)");
                        }
                        else if (option == 'e')
                        {
                            onlyIfNotExist = true;
                        }
                        else if (option == 'w')
                        {
                            webAuthentication = true;
                        }
                        else if (option == 'r')
                        {
                            noRedirect = true;
                        }
                        else if (option == 'i')
                        {
                            ignoreSslErrors = true;
                        }
                        else if (option == 'b')
                        {
                            a++;
                            if (a >= args.Length)
                            {
                                Console.WriteLine(badOption, arg);
                                return false;
                            }
                            else
                            {
                                bearerToken = args[a];
                            }
                        }
                    }
                    else
                    {
                        // The argument is not an option, so it must be the source URL.
                        // If we reach here several times, it's probably because source URL was specified with embedded spaces.
                        // In that case simply paste the URL together (assuming there's only a single space between them).
                        if (srcUrl == null)
                        {
                            srcUrl = arg;
                        }
                        else
                        {
                            unquotedSrcUrl = true;
                            srcUrl += "%20" + arg;
                        }
                    }
                }
            }

            // The only mandatory input is the source URL.
            if (srcUrl == null)
            {
                Console.WriteLine(usage);
                return false;
            }
            else if (unquotedSrcUrl && (verbose || veryverbose))
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Warning: The source URL was constructed via heuristics; use quotes if it's wrong.");
                Console.ForegroundColor = color;
            }

            // Cleanup the source URL
            srcUrl = srcUrl.Trim(' ', '"');

            return true;
        }

        string NormalizeUrl(string url)
        {
            if (url == "CON" || url == "CON:" || url == "console")
            {
                return "console";
            }
            return url;
        }

        void DetermineDstUrl(string fromUrl)
        {
            fromUrl = NormalizeUrl(fromUrl);
            dstUrl = NormalizeUrl(dstUrl);
            
            if (!autoDstUrl)
            {
                return; // Nothing to do -- the user has already specified it
            }

            if (fromUrl == "console")
            {
                dstUrl = fromUrl;
                return;
            }

            Uri srcUri = new Uri(fromUrl);

            string resName = srcUri.Segments[srcUri.Segments.Length - 1];
            if (resName == null || resName == "/" || resName == "\\")
            {
                resName = "root.html";
            }

            // If the resource name has embedded encoded spaces in it (%20),
            // convert them to real spaces.
            // (We ignore other types of encoded characters etc.)
            resName = resName.Replace("%20", " ");

            // If the resource name has slashes in it, remove them.
            // This can be, for example, if the resource is a "directory".
            resName = resName.Replace("/", "");

            if (string.IsNullOrEmpty(resName))
            {
                resName = "root.html";
            }

            string localName = System.Environment.CurrentDirectory + "\\" + resName;

            // Cycle through the names in the current directory,
            // and make sure that localName doesn't exist.
            int index = 0;
            while (System.IO.File.Exists(localName))
            {
                if (onlyIfNotExist)
                {
                    Console.WriteLine("Ignoring file as it already exists locally and -e was specified");
                    dstUrl = null;
                    return;
                }

                index++;
                // TOOD: Switch the index and the file extension
                // TODO: Fail after a number of trials?
                localName = System.Environment.CurrentDirectory + "\\" + resName + "." + index;
            }

            dstUrl = localName;
        }

        void Dump(string banner, System.Net.Http.Headers.HttpHeaders headers)
        {
            string separator = "  ----------------------------------------------";

            Console.WriteLine(banner);
            Console.WriteLine(separator);

            foreach (var kvp in headers)
            {
                Console.WriteLine("  {0}={1}", kvp.Key, String.Join(Environment.NewLine + "    ", kvp.Value));
            }
            Console.WriteLine(separator);
            Console.WriteLine("");
        }

        void Run()
        {
            if (Debugger.IsAttached)
            {
                // Run without a try/catch. This will have the debugger stop at the point of crash
                // on failures.

                // RunImpl();
                // return;
            }

            // No debugger attached, so use a try/catch to provide maximum information
            // in terms the operation fails
            try
            {
                RunImpl();
            }
            catch (Exception e)
            {
                Console.WriteLine("ZGET download failure! The following exception was caught:");
                Console.WriteLine(e.ToString());
                Console.WriteLine(e.StackTrace);

                // Additional information printed if verbose
                if (this.verbose)
                {
                    System.Net.WebException webEx = e as System.Net.WebException;
                    if (webEx != null)
                    {
                        var resp = webEx.Response;
                        if (resp != null)
                        {
                            Console.WriteLine("--- Headers ---");
                            var respHeaders = resp.Headers;
                            for (int i = 0; i < respHeaders.Count; i++)
                            {
                                var key = respHeaders.GetKey(i);
                                Console.WriteLine("Header: {0}: {1}", respHeaders.GetKey(i), respHeaders[key]);
                            }

                            var respStream = resp.GetResponseStream();
                            var reader = new StreamReader(respStream);
                            using (reader)
                            {
                                // We do not expect overly long responses, so this is probably okay
                                var response = reader.ReadToEnd();
                                Console.WriteLine("--- Body ---");
                                Console.WriteLine(response);
                            }
                        }
                    }

                    System.IO.IOException ioEx = e as System.IO.IOException;
                    if (ioEx != null)
                    {
                        Console.WriteLine("This is an IO Exception. Either the target folder is not accessible, or the destination name is not properly formatter.");
                    }
                }
            }
        }

        private void RunImpl()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            bool fDumpProgress = verbose;
            bool fDumpHeaders = veryverbose;


            if (fDumpProgress)
            {
                Console.WriteLine("ZGET v1.0 download starts:");
            }

            Uri srcUri = new Uri(srcUrl);

            if (fDumpProgress)
            {
                Console.WriteLine("  (Contacting server ...{0})", srcUri);
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            using (var clientHandler = new System.Net.Http.SocketsHttpHandler())
            {
                if (proxyServer != null)
                {
                    clientHandler.UseProxy = true;
                    clientHandler.Proxy = new System.Net.WebProxy(proxyServer);
                    // clientHandler.DefaultProxyCredentials
                }

                clientHandler.AllowAutoRedirect = !noRedirect;

                clientHandler.ConnectCallback = (System.Net.Http.SocketsHttpConnectionContext context, System.Threading.CancellationToken cancellationToken) =>
                {
                    var s = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                        s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 5);
                        s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
                        s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
                        s.Connect(context.DnsEndPoint.Host, context.DnsEndPoint.Port);
                        return System.Threading.Tasks.ValueTask.FromResult((Stream)new NetworkStream(s, ownsSocket: true));
                    }
                    catch
                    {
                        s.Dispose();
                        throw;
                    }
                };

                if (verbose)
                {
                    // First we create a fake connection to the service just to see what SSL options are established.
                    if (srcUri.Scheme == Uri.UriSchemeHttps)
                    {
                        using (var tcpClient = new TcpClient(srcUri.IdnHost, srcUri.Port))
                        {
                            using (var sslStream = new System.Net.Security.SslStream(tcpClient.GetStream(), leaveInnerStreamOpen: false, (sender, cert, chain, errors) => true))
                            {
                                var sslClientAuthenticationOptions = new System.Net.Security.SslClientAuthenticationOptions();
                                sslClientAuthenticationOptions.TargetHost = srcUri.IdnHost;
                                sslClientAuthenticationOptions.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;

                                sslStream.AuthenticateAsClient(sslClientAuthenticationOptions);

                                Console.WriteLine($"  TLS: Protocol                      = {sslStream.SslProtocol}");
                                Console.WriteLine($"  TLS: Cipher                        = {sslStream.CipherAlgorithm}");
                                Console.WriteLine($"  TLS: CipherStrength                = {sslStream.CipherStrength}");
                                Console.WriteLine($"  TLS: Hash                          = {sslStream.HashAlgorithm}");
                                Console.WriteLine($"  TLS: HashStrength                  = {sslStream.HashStrength}");
                                Console.WriteLine($"  TLS: KeyExchange                   = {sslStream.KeyExchangeAlgorithm}");
                                Console.WriteLine($"  TLS: KeyExchangeStrength           = {sslStream.KeyExchangeStrength}");
                                Console.WriteLine($"  TLS: NegotiatedApplicationProtocol = {sslStream.NegotiatedApplicationProtocol}");
                                Console.WriteLine($"  TLS: NegotiatedCipherSuite         = {sslStream.NegotiatedCipherSuite}");
                            }
                        }
                    }

                    var sslOptions = new System.Net.Security.SslClientAuthenticationOptions();
                    sslOptions.RemoteCertificateValidationCallback = (object sender, X509Certificate cert, X509Chain chain, System.Net.Security.SslPolicyErrors errors) =>
                    {
                        // Write out properties that are always there
                        Console.WriteLine("  Server cert: Issuer   ={0}", cert.Issuer);
                        Console.WriteLine("  Server cert: Subject  ={0}", cert.Subject);
                        Console.WriteLine("  Server cert: Errors   ={0}", errors.ToString());

                        var cert2 = cert as X509Certificate2;
                        if (cert2 != null)
                        {
                            Console.WriteLine("  Server cert: NotBefore={0}", cert2.NotBefore.ToString("G"));
                            Console.WriteLine("  Server cert: NotAfter ={0}", cert2.NotAfter.ToString("G"));

                            // Write out all get properties whose type is a string that we haven't written out previously
                            foreach (var property in cert2.GetType().GetProperties())
                            {
                                var name = property.Name;
                                if (name != "Issuer" && name != "Subject")
                                {
                                    var getMethod = property.GetGetMethod();
                                    if (getMethod != null && getMethod.ReturnType == typeof(string))
                                    {
                                        var value = getMethod.Invoke(cert2, null);
                                        Console.WriteLine("  Server cert: {0}={1}", property.Name, value ?? "<null>");
                                    }
                                }
                            }

                            // Write out the extensions
                            // (e.g., the SAN (Subject Alternate Name) extension is oid.Value.Equals("2.5.29.17")
                            foreach (var extension in cert2.Extensions)
                            {
                                var asnEncodedData = new AsnEncodedData(extension.Oid, extension.RawData);
                                Console.WriteLine("  Server cert: Extension type: {0}", extension.Oid.FriendlyName);
                                var prefix = "                  ";
                                Console.WriteLine(prefix + "Oid value: {0}", asnEncodedData.Oid.Value);
                                Console.WriteLine(prefix + "Raw data length: {0}", asnEncodedData.RawData.Length);

                                Console.WriteLine(prefix + "Data");
                                var data = asnEncodedData.Format(multiLine: true);
                                data = prefix + "  " + data.Replace(Environment.NewLine, Environment.NewLine + prefix + "  ");
                                Console.WriteLine(data);
                            }
                        }

                        Console.WriteLine("  Server cert check: {0}", errors.ToString());

                        if (ignoreSslErrors)
                        {
                            return true;
                        }

                        return errors == System.Net.Security.SslPolicyErrors.None;

                    };
                    clientHandler.SslOptions = sslOptions;
                }

                using (var client = new System.Net.Http.HttpClient(clientHandler, disposeHandler: false))
                {
                    var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, srcUri);
                    if (bearerToken != null)
                    {
                        req.Headers.Add("Authorization", $"bearer {bearerToken}");
                    }

                    req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html"));
                    req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
                    req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));

                    var resp = client.Send(req);
                    if (!resp.IsSuccessStatusCode)
                    {
                        if (resp.StatusCode == HttpStatusCode.Moved // 301
                            || resp.StatusCode == HttpStatusCode.MovedPermanently // 301
                            || resp.StatusCode == HttpStatusCode.Found // 302
                            || resp.StatusCode == HttpStatusCode.Redirect) // 302
                        {
                            Console.WriteLine("Moved/Redirect: {0} {1}", (int)resp.StatusCode, resp.ReasonPhrase);
                            Console.WriteLine("Location: {0}", resp.Headers.Location);
                        }
                        else
                        {
                            Console.WriteLine("HTTP error: {0} {1}", (int)resp.StatusCode, resp.ReasonPhrase);
                            // TODO: Write more information about the error, such as the headers and the body...
                        }
                        return;
                    }

                    // Dump headers
                    // TODO: Can the URI we're getting back on the response be differe than the one we gave it?
                    if (fDumpProgress)
                    {
                        Console.WriteLine("  URL: {0}", srcUrl);
                        Console.WriteLine("  TGT: {0}", dstUrl);
                        if (proxyServer != null)
                        {
                            Console.WriteLine("PROXY: {0}", proxyServer);
                        }
                    }

                    if (fDumpHeaders)
                    {
                        Dump("  Request headers:", req.Headers);
                    }

                    Console.WriteLine("  StatusCode={0}, ReasonPhrase={1}", resp.StatusCode, resp.ReasonPhrase);

                    if (fDumpHeaders)
                    {
                        Dump("  Response headers:", resp.Headers);
                    }

                    // If this is a redirect response, we've just written it out
                    // (and we're not following redirects), so just quit.
                    if ((int)resp.StatusCode >= 300 && (int)resp.StatusCode < 400)
                    {
                        return;
                    }

                    // 
                    Stream input = resp.Content.ReadAsStream();

                    long? nContentLength = resp.Content.Headers.ContentLength;

                    long nTotalBytes = 0;

                    DetermineDstUrl(srcUrl);
                    if (dstUrl == "console")
                    {
                        using (var inputReader = new StreamReader(input))
                        {
                            while (!inputReader.EndOfStream)
                            {
                                var line = inputReader.ReadLine();
                                Console.WriteLine(line);
                            }
                        }
                    }
                    else
                    {
                        BinaryReader inputReader = new BinaryReader(input);
                        using (FileStream output = new FileStream(dstUrl, FileMode.OpenOrCreate))
                        {
                            using (BinaryWriter outputWriter = new BinaryWriter(output))
                            {
                                int BufferSize = 65536;
                                Byte[] buffer = new Byte[BufferSize];

                                while (true)
                                {
                                    int nBytes = inputReader.Read(buffer, 0, BufferSize);
                                    nTotalBytes += nBytes;

                                    outputWriter.Write(buffer, 0, nBytes);

                                    if (fDumpProgress && nBytes != 0)
                                    {
                                        // TODO: This used to be {0:n0} {1:n0}, ... but in .NET 4.5 the 'n' doesn't work for me
                                        //       See also printing below

                                        Console.WriteLine("{0} sec elapsed, {1} bytes received (out of {2}), last buffer: {3} bytes",
                                            stopWatch.Elapsed,
                                            nTotalBytes,
                                            nContentLength,
                                            nBytes
                                        );
                                    }

                                    if (nBytes == 0)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    TimeSpan overall = stopWatch.Elapsed;
                    double msecOverall = overall.TotalMilliseconds;
                    double downloadRate = (nTotalBytes * 8.0) * 1000 / msecOverall;

                    // TODO: This also used to have {0:n0} etc.
                    Console.WriteLine("\"{0}\" downloaded successfully. {1} bytes in {2} sec ({3} Kbps) [{4} msec].",
                        dstUrl,
                        nTotalBytes,
                        msecOverall / 1000,
                        downloadRate / 1000,
                        msecOverall);

                    if (autostart)
                    {
                        if (dstUrl == "console")
                        {
                            Console.WriteLine("Error: Can't open the console for reading...");
                        }
                        else
                        {
                            Console.WriteLine("Attempting to start the file...");

                            ProcessStartInfo psi = new ProcessStartInfo();
                            psi.FileName = dstUrl;
                            psi.UseShellExecute = true;
                            psi.Verb = "open";
                            System.Diagnostics.Process.Start(psi);
                        }
                    }
                }
            }

        } // RunImpl
    }
}
