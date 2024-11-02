using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

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

    If <dstUrl> is not specified, the resource will be stored in the current
    directory using a unique name derived from <srcUrl>.

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
                            object data = Clipboard.GetData(DataFormats.StringFormat);
                            srcUrl = data as string;
                            if (string.IsNullOrEmpty(srcUrl))
                            {
                                Console.WriteLine(badClipboard);
                                return false;
                            }
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

        void DetermineDstUrl(string fromUrl)
        {
            if (!autoDstUrl)
            {
                return; // Nothing to do -- the user has already specified it
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

        void Dump(string banner, WebHeaderCollection headers)
        {
            string separator = "  ----------------------------------------------";

            Console.WriteLine(banner);
            Console.WriteLine(separator);

            foreach (string key in headers.Keys)
            {
                Console.WriteLine("  {0}={1}", key, headers[key]);
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
                RunImpl();
                return;
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
            // Configure security settings
            System.Net.ServicePointManager.CheckCertificateRevocationList = true;
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            // Make sure all numbers are formatted nicely.
            // By doing this, we get that whenever someone uses ToString("n"), they get
            // the same as ToString("n0"). Equivalently, they get the effect of:
            // Console.WriteLine("{0:n00}", 123456); --> 123,456
            // TODO: How do we do this? It doesn't really work!
            // NumberFormatInfo nfi = new CultureInfo(CultureInfo.CurrentCulture.Name, false).NumberFormat;
            //nfi.NumberDecimalDigits = 0;

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            bool fDumpProgress = verbose;
            bool fDumpHeaders = veryverbose;


            if (fDumpProgress)
            {
                Console.WriteLine("ZGET v1.0 download starts:");
            }

            bool isSharepointReader = false;
            srcUrl = TweakSrcUrlForSharepointReader(srcUrl, out isSharepointReader);

            Uri srcUri = new Uri(srcUrl);

            if (fDumpProgress)
            {
                Console.WriteLine("  (Contacting server ...{0})", srcUri);
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // TODO: Replace WebRequest with HttpClient
            WebRequest req = WebRequest.Create(srcUri);

            // Some request types (such as ftp:)
            // do not support setting default credentials.
            // Currently I don't know how am I supposed to
            // know which one does and which one doesn't,
            // so I'm using a blacklist.
            if (srcUri.Scheme != "ftp")
            {
                // TODO: We should be able to do this automatically once ADAL is in place,
                //       instead of asking the user to hint us.
                if (webAuthentication && srcUri.Scheme == "https")
                {
                    // Old code, doesn't work anymore:
                    //var username = "zivc@microsoft.com";
                    //Console.WriteLine("Enter username (hit 'Enter' for {0}):", username);
                    //string anotherUsername = Console.ReadLine();
                    //if (!string.IsNullOrWhiteSpace(anotherUsername))
                    //{
                    //    username = anotherUsername;
                    //}

                    //Console.WriteLine("Enter password for {0}:", username);
                    //string password = Console_ReadPassword(true);

                    //MsOnlineClaimsHelper.AddAuthenticationCookieToWebRequest(username, password, srcUri.AbsoluteUri, (HttpWebRequest)req);

                    // TODO: Replace this with new code to prompt users for credentials via MSAL
                    //       and add a bearer token
                }
                else
                {
                    req.Credentials = CredentialCache.DefaultCredentials;
                }
            }

            if (proxyServer != null)
            {
                req.Proxy = new WebProxy(proxyServer);
            }

            if (bearerToken != null)
            {
                req.Headers.Add(HttpRequestHeader.Authorization, $"bearer {bearerToken}");
            }

            HttpWebRequest hwr = req as HttpWebRequest;
            if (hwr != null)
            {
                hwr.Accept = "text/html, application/xhtml+xml, */*";
                hwr.AllowAutoRedirect = !noRedirect;

                hwr.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36 Edge/16.16299";

                if (verbose)
                {
                    System.Net.ServicePointManager.ServerCertificateValidationCallback = (obj, cert, chain, errors) =>
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
                }
            }

            WebResponse resp = req.GetResponse();
            Stream input = resp.GetResponseStream();
            BinaryReader inputReader = new BinaryReader(input);

            // Determine the destination URL we want to write to.
            // Note that the resource URL that we used to download
            // and the one we get from the response might differ.
            // For example, this may happen if we have a redirect
            // done by some server-side code.
            // If we've tweaked the srcUrl due to Sharepoint stuff, the response URI
            // will still point at the Sharepoint crap, so we have to rely on the
            // srcUrl.
            if (!isSharepointReader && !string.IsNullOrEmpty(resp.ResponseUri.AbsolutePath))
            {
                DetermineDstUrl(resp.ResponseUri.AbsoluteUri);
            }
            else
            {
                DetermineDstUrl(srcUrl);
            }

            if (dstUrl == null)
            {
                // Nothing left to do
                return;
            }

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

            var hwresp = resp as HttpWebResponse;
            if (hwresp != null && fDumpHeaders)
            {
                Console.WriteLine("  StatusCode={0}, StatusDescription={1}", hwresp.StatusCode, hwresp.StatusDescription);
            }

            if (fDumpHeaders)
            {
                Dump("  Response headers:", resp.Headers);
            }

            // If this is a redirect response, we've just written it out
            // (and we're not following redirects), so just quit.
            if (hwresp != null && (int)hwresp.StatusCode >= 300 && (int)hwresp.StatusCode < 400)
            {
                return;
            }

            long nContentLength = resp.ContentLength; // Note that this may not be set always

            long nTotalBytes = 0;
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
                Console.WriteLine("Attempting to start the file...");

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = dstUrl;
                psi.UseShellExecute = true;
                psi.Verb = "open";
                System.Diagnostics.Process.Start(psi);
            }
        } // RunImpl

        private string Console_ReadPassword(bool asterixes)
        {
            string password = "";
            do
            {
                ConsoleKeyInfo cki = Console.ReadKey(true);
                //Console.WriteLine("Mod={0}, key={1}, keychar={2}", cki.Modifiers, cki.Key, cki.KeyChar);
                if (cki.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine("<Cleared by ESC>");
                    password = "";
                    continue;
                }
                if (cki.Modifiers == 0 || cki.Modifiers == ConsoleModifiers.Shift)
                {
                    if (cki.Key == ConsoleKey.Enter)
                    {
                        break;
                    }

                    char c = cki.KeyChar;
                    if (!Char.IsControl(c))
                    {
                        if (asterixes)
                        {
                            Console.Write("*");
                        }
                        password += c;
                    }
                }
            }
            while (true);

            if (asterixes)
            {
                Console.WriteLine();
            }

            return password;
        }

        static string[] s_officeViewers = { "PowerPoint.aspx", "OneNote.aspx", "WordViewer.aspx", "WopiFrame.aspx", "xlviewer.aspx" };
        static string[] s_officeViewersQueryItems = { "PresentationId", "id", "id", "sourcedoc", "id" };
        static int IndexOf(string[] values, string value)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == value)
                {
                    return index;
                }
            }
            return -1;
        }

        private string TweakSrcUrlForSharepointReader(string srcUrl, out bool isSharepointReader)
        {
            isSharepointReader = false;
            Uri srcUri = new Uri(srcUrl);
            if (srcUri.Scheme != "http" && srcUri.Scheme != "https")
            {
                return srcUrl;
            }

            // We identify Sharepoint reader in the following manner:
            // The end of the Url is .../_layouts/PowerPoint.aspx or .../_layouts/Word.aspx or .../_layouts/OneNote.aspx (?)
            // or .../_layouts/15/WopiFrame.aspx?
            // The query string begins with "?PowerPointView=ReadingView&PresentationId=
            if (srcUri.Segments == null)
            {
                return srcUrl;
            }
            int nSegments = srcUri.Segments.Length;
            if (nSegments < 2)
            {
                return srcUrl;
            }
            // TODO: This is no longer true in O15:
            //            if (srcUri.Segments[nSegments-2] != "_layouts/")
            //            {
            //                return srcUrl;
            //            }
            string viewer = srcUri.Segments[nSegments - 1];
            int viewerIndex = IndexOf(s_officeViewers, viewer);
            if (viewerIndex == -1)
            {
                if (srcUri.Query.Contains("Web=1"))
                {
                    // TODO: This is really ugly code. Replace it with something better:
                    srcUrl = srcUrl.Replace("Web=1", "");
                    srcUrl = srcUrl.TrimEnd('?'); // Don't leave a last quote mark all alone
                }
                // Note an Office viewer URL, just use what you have
                return srcUrl;
            }

            // Looks like an Office viewer URL. We convert it to the actual doc URL.

            var query = System.Web.HttpUtility.ParseQueryString(srcUri.Query);
            string docPath = query[s_officeViewersQueryItems[viewerIndex]];
            if (string.IsNullOrEmpty(docPath))
            {
                // The query item ("PresentationId=..." or just "id=...") is not found.
                // SrcUrl is probably wrong here too, but at least that's the best we have.
                return srcUrl;
            }

            if (verbose)
            {
                Console.WriteLine("  Sharepoint reader URL detected: {0}", s_officeViewers[viewerIndex]);
            }

            isSharepointReader = true;
            srcUrl = srcUri.GetLeftPart(UriPartial.Authority) + docPath;
            return srcUrl;

            // http://sharepoint/sites/ISD/_layouts/PowerPoint.aspx?PowerPointView=ReadingView
            // &PresentationId=/sites/ISD/Brownbags/2011-%2007-07%20-%20Haris%20Majeed%20-%20Identity%20in%20AWS%20and%20GAE.pptx
            // &Source=http%3A%2F%2Fsharepoint%2Fsites%2FISD%2FBrownbags%2FForms%2FDefault1%2Easpx&DefaultItemOpen=1

            // http://office/15/specs/_layouts/15/WopiFrame.aspx?sourcedoc=/15/specs/Specs/BI/Photo%20Vote%20Operations/Photo%20Vote%20Operations%20Handbook.docx&action=default&Source=http%3A%2F%2Foffice%2F15%2Fspecs%2FSpecs%2FForms%2FSpec%2520Document%2520Set%2Fdocsethomepage%2Easpx%3FID%3D26428%26FolderCTID%3D0x0120D52000BF7AE52AD652344B93DE777853D5AABB0097300CFF571BDF45A40B0E9EE13A9F19%26List%3D79fb6676%2D3f9b%2D4ae2%2Daabe%2De47f4316b2ee%26RootFolder%3D%252F15%252Fspecs%252FSpecs%252FBI%252FPhoto%2520Vote%2520Operations&DefaultItemOpen=1
        }
    }
}
