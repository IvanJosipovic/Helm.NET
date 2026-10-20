using System.Runtime.InteropServices;

namespace Helm.NET;

internal interface IHelmNativeApi
{
    nint Execute(nint input);
    string? GetString(nint value);
    void Free(nint value);
}

internal static class HelmNativeInterop
{
    static HelmNativeInterop()
    {
        NativeLibrary.SetDllImportResolver(typeof(HelmNativeInterop).Assembly, ResolveLibrary);
    }

    internal static IHelmNativeApi DefaultApi { get; } = new NativeHelmApi();

    private static nint ResolveLibrary(
        string libraryName,
        System.Reflection.Assembly _assembly,
        DllImportSearchPath? _searchPath)
    {
        if (!string.Equals(libraryName, "helmshim", StringComparison.Ordinal))
            return 0;

        var libraryPath = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            RuntimeInformation.RuntimeIdentifier,
            "native",
            GetLibraryFileName());

        return NativeLibrary.TryLoad(libraryPath, out var handle) ? handle : nint.Zero;
    }

    private static string GetLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
            return "helmshim.dll";

        return OperatingSystem.IsMacOS() ? "libhelmshim.dylib" : "libhelmshim.so";
    }

    internal static string Invoke(byte[] payload, IHelmNativeApi nativeApi)
    {
        var input = Marshal.AllocHGlobal(payload.Length + 1);
        try
        {
            Marshal.Copy(payload, 0, input, payload.Length);
            Marshal.WriteByte(input, payload.Length, 0);
            var resultPointer = nativeApi.Execute(input);
            if (resultPointer == 0)
                throw new InvalidOperationException("The Helm native library returned a null response.");

            try
            {
                return nativeApi.GetString(resultPointer) ?? throw new InvalidOperationException("The Helm native library returned an invalid response.");
            }
            finally
            {
                nativeApi.Free(resultPointer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(input);
        }
    }

    private sealed class NativeHelmApi : IHelmNativeApi
    {
        public nint Execute(nint input) => NativeMethods.HelmExecuteJson(input);
        public string? GetString(nint value) => Marshal.PtrToStringUTF8(value);
        public void Free(nint value) => NativeMethods.HelmFree(value);

        private static class NativeMethods
        {
            [DllImport("helmshim", EntryPoint = "HelmExecuteJSON", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern nint HelmExecuteJson(nint input);

            [DllImport("helmshim", EntryPoint = "HelmFree", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern void HelmFree(nint value);
        }
    }
}
