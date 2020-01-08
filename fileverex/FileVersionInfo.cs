using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

#region ExtendedFileVersionInfo
/// <summary>
/// This class allows one to query arbitrary string values out of a file's VersionInfo
/// structure. This fills the 'holes' in the implementation of FileVersionInfo.
/// </summary>
public sealed class ExtendedFileVersionInfo : IDisposable
{
    private static class NativeMethods
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api", Justification = "Managed - yes, Equivalent - no.")]
        [System.Runtime.InteropServices.DllImport("version.dll", SetLastError = true, CharSet = CharSet.None, BestFitMapping = false)]
        public static extern int GetFileVersionInfoSize(string sFileName, out int handle);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api", Justification = "Managed - yes, Equivalent - no.")]
        [System.Runtime.InteropServices.DllImport("version.dll", SetLastError = true, CharSet = CharSet.None, BestFitMapping = false)]
        public static extern int GetFileVersionInfo(string sFileName, int handle, int size, IntPtr buffer);

        [System.Runtime.InteropServices.DllImport("version.dll", SetLastError = false, CharSet = CharSet.None, BestFitMapping = false)]
        unsafe public static extern int VerQueryValue(IntPtr pBlock, string pSubBlock, out IntPtr pValue, out uint len);

        [System.Runtime.InteropServices.DllImport("version.dll", SetLastError = false, CharSet = CharSet.None, BestFitMapping = false)]
        unsafe public static extern int VerQueryValue(IntPtr pBlock, string pSubBlock, out short* pValue, out uint len);
    }

    private string m_filename;
    private int m_handle;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources")]
    private IntPtr m_buffer;
    private int m_langid;
    private int m_codepage;

    /// <summary>
    /// ExtendedFileVersionInfo factory
    /// </summary>
    /// <param name="fileName">The name of the PE file whose version resource we want to read.</param>
    /// <returns>
    /// Returns instance of ExtendedFileVersionInfo when it is present in fileName and was successfully read; and null - otherwise.
    /// </returns>
    unsafe static public ExtendedFileVersionInfo TryCreate(string fileName)
    {
        if (!ExtendedEnvironment.IsWindows)
        {
            return null;
        }

        try
        {
            return new ExtendedFileVersionInfo(fileName);
        }
        catch (ExtendedFileVersionInfoException)
        {
            return null;
        }
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="fileName">The name of the PE file whose version resource we want to read.</param>
    unsafe private ExtendedFileVersionInfo(string fileName)
    {
        // Determine how much room to make for the version resource
        m_filename = fileName;
        int size = NativeMethods.GetFileVersionInfoSize(m_filename, out m_handle);
        if (size == 0)
        {
            int error = Marshal.GetLastWin32Error();
            // ERROR_RESOURCE_DATA_NOT_FOUND, ERROR_RESOURCE_TYPE_NOT_FOUND
            throw new ExtendedFileVersionInfoException(fileName: m_filename, function: "GetFileVersionInfoSize", errorCode: error);
        }

        // Load the version resource into memory
        m_buffer = Marshal.AllocCoTaskMem(size);
        if (0 == NativeMethods.GetFileVersionInfo(m_filename, m_handle, size, m_buffer))
        {
            int error = Marshal.GetLastWin32Error();
            //UtilsTrace.Tracer.TraceWarning("GetFileVersionInfo({0}) failed: {1}",
            //     m_filename, error);
            throw new ExtendedFileVersionInfoException(fileName: m_filename, function: "GetFileVersionInfo", errorCode: error);
        }

        // Determine the translation (langid and codepage)
        short* subBlock = null;
        uint len = 0;
        if (0 == NativeMethods.VerQueryValue(m_buffer, @"\VarFileInfo\Translation", out subBlock, out len))
        {
            //UtilsTrace.Tracer.TraceWarning("VerQueryValue({0}, \\VarFileInfo\\Translation) failed",
            //     m_filename);
            throw new ExtendedFileVersionInfoException(m_filename, "VerQueryValue on \\VarFileInfo\\Translation");
        }

        m_langid = subBlock[0];
        m_codepage = subBlock[1];
    }

    /// <summary>
    /// Destructor
    /// </summary>
    ~ExtendedFileVersionInfo()
    {
        Dispose(false);
    }

    /// <summary>
    /// Gets the named string from the version resource.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations",
        Justification = "Here this behaviour is desired by design.")]
    private unsafe string this[string name]
    {
        get
        {
            string result;
            if (!TryGetVersionString(name, out result))
            {
                throw new ExtendedFileVersionInfoException(m_filename, "VerQueryValue on " + GetSpvString(name));
            }
            return result;
        }
    }

    /// <summary>
    /// Trying to get the string from the version resource.
    /// </summary>
    /// <param name="name">Name of the resource property</param>
    /// <param name="fileName">The name of the PE file whose version resource we want to read.</param>        
    /// <returns>True if such string is found, otherwise false</returns>       
    unsafe public static string GetVersionString(string fileName, string name)
    {
        //Ensure.ArgIsNotNullOrWhiteSpace(fileName, "fileName");
        //Ensure.ArgIsNotNullOrWhiteSpace(name, "name");

        if (!ExtendedEnvironment.IsWindows)
        {
            return null;
        }

        using (var info = new ExtendedFileVersionInfo(fileName))
        {
            return info[name];
        }
    }

    /// <summary>
    /// Trying to get the string from the version resource.
    /// </summary>
    /// <param name="name">Name of the resource property</param>
    /// <param name="fileName">The name of the PE file whose version resource we want to read.</param>
    /// <param name="result">Holds the version string</param>
    /// <returns>True if such string is found, otherwise false</returns>
    unsafe public static bool TryGetVersionString(string fileName, string name, out string result)
    {
        //Ensure.ArgIsNotNullOrWhiteSpace(fileName, "fileName");
        //Ensure.ArgIsNotNullOrWhiteSpace(name, "name");

        if (!ExtendedEnvironment.IsWindows)
        {
            result = null;
            return false;
        }

        try
        {
            using (var info = new ExtendedFileVersionInfo(fileName))
            {
                return info.TryGetVersionString(name, out result);
            }
        }
        catch (ExtendedFileVersionInfoException)
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Trying to get the string from the version resource.
    /// </summary>
    /// <param name="name">Name of the resource property</param>
    /// <param name="result">Holds the version string</param>
    /// <returns>True if such string is found, otherwise false</returns>
    unsafe public bool TryGetVersionString(string name, out string result)
    {
        //Ensure.ArgIsNotNullOrWhiteSpace(name, "name");
        Debug.Assert(m_buffer != null);

        if (!ExtendedEnvironment.IsWindows)
        {
            result = null;
            return false;
        }

        // To query for a given resouce name, we need to use the following 'magic':
        string spv = GetSpvString(name);

        uint len;
        IntPtr value;
        if (0 == NativeMethods.VerQueryValue(m_buffer, spv, out value, out len))
        {
            //Wpp.Trace(Wpp.Error, Wpp.FlagComponent, "GetFileVersionInfoSize(%s, %s) failed: version string not found", m_filename, spv);
            result = null;
            return false;
        }

        // Convert the string pointed-to by 'value' to a real string
        if (len > 0)
        {
            // TODO: Apparently len also contains a terminating '\0'. Need to check.
            --len;
        }
        result = Marshal.PtrToStringAnsi(value, (int)len);
        return true;
    }

    /// <summary>
    /// Dispose of the object
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    unsafe private void Dispose(bool disposing)
    {
        if (disposing)
        {
            // No managed resources to free, actually
        }

        if (m_buffer.ToPointer() != null)
        {
            Marshal.FreeCoTaskMem(m_buffer);
        }
    }

    private string GetSpvString(string name)
    {
        string spv = string.Format(
            CultureInfo.InvariantCulture,
            @"\StringFileInfo\{0:X4}{1:X4}\{2}",
            m_langid,
            m_codepage,
            name);
        return spv;
    }
}
#endregion

#region class ExtendedFileVersionInfoException
class ExtendedFileVersionInfoException : Exception
{
    public string FileName;
    public string Function;
    public int? ErrorCode;

    public ExtendedFileVersionInfoException(string fileName, string function, int? errorCode = null)
    {
        FileName = fileName;
        Function = function;
        ErrorCode = errorCode;
    }

    public override string Message => 
        $"FileVersionInfo could not be read. ErrorCode={ErrorCode}, Function={Function}, FileName={FileName}";
}
#endregion

#region class ExtendedEnvironment
public static class ExtendedEnvironment
{
    public static bool IsWindows => true; // For now
}
#endregion
