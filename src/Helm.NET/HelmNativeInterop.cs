using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Helm.NET;

internal interface IHelmNativeApi
{
    NativeHelmResponse Execute(NativeHelmRequest request);
}

internal static partial class HelmNativeInterop
{
    private const uint AbiVersion = 1;
    private const int ResultAck = 1;
    private const int ResultText = 2;
    private const int ResultJson = 3;
    private const int ResultRelease = 4;
    private const int ResultReleases = 5;
    private const int ResultRepositories = 6;

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

    private sealed partial class NativeHelmApi : IHelmNativeApi
    {
        public NativeHelmResponse Execute(NativeHelmRequest request)
        {
            using var input = new NativeRequestBuffer();
            var valuesJson = request.Values is { } values && values.ValueKind != JsonValueKind.Undefined
                ? JsonSerializer.Serialize(values, NativeJsonContext.Default.JsonElement)
                : null;
            var nativeRequest = new NativeHelmRequestAbi
            {
                AbiVersion = AbiVersion,
                Operation = input.AddString(request.Operation),
                ReleaseName = input.AddString(request.ReleaseName),
                Chart = input.AddString(request.Chart),
                ValuesFiles = new NativeStringArrayAbi
                {
                    Items = input.AddStringArray(request.ValuesFiles),
                    Length = (nuint)(request.ValuesFiles?.Length ?? 0)
                },
                ValuesJson = input.AddString(valuesJson),
                CreateNamespace = request.CreateNamespace ? 1 : 0,
                Wait = request.Wait ? 1 : 0,
                TimeoutMilliseconds = request.TimeoutMilliseconds,
                Kubeconfig = input.AddString(request.Kubeconfig),
                KubeContext = input.AddString(request.KubeContext),
                Namespace = input.AddString(request.Namespace),
                AllNamespaces = request.AllNamespaces ? 1 : 0,
                Filter = input.AddString(request.Filter),
                Revision = request.Revision,
                InstallIfMissing = request.InstallIfMissing ? 1 : 0,
                RepositoryConfig = input.AddString(request.RepositoryConfig),
                RepositoryCache = input.AddString(request.RepositoryCache),
                RegistryConfig = input.AddString(request.RegistryConfig),
                HelmDriver = input.AddString(request.HelmDriver),
                RepositoryName = input.AddString(request.RepositoryName),
                RepositoryUrl = input.AddString(request.RepositoryURL),
                RepositoryUsername = input.AddString(request.RepositoryUsername),
                RepositoryPassword = input.AddString(request.RepositoryPassword)
            };

            var requestPointer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeHelmRequestAbi>());
            try
            {
                Marshal.StructureToPtr(nativeRequest, requestPointer, false);
                var responsePointer = NativeMethods.HelmExecuteV1(requestPointer);
                if (responsePointer == 0)
                    throw new InvalidOperationException("The Helm native library returned a null response.");

                try
                {
                    return ReadResponse(responsePointer);
                }
                finally
                {
                    NativeMethods.HelmFreeResponseV1(responsePointer);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(requestPointer);
            }
        }

        private static NativeHelmResponse ReadResponse(nint responsePointer)
        {
            var response = Marshal.PtrToStructure<NativeHelmResponseAbi>(responsePointer);
            if (response.AbiVersion != AbiVersion)
                throw new InvalidOperationException($"Unsupported Helm native ABI version: {response.AbiVersion}.");

            var error = ReadString(response.Error);
            if (error is not null)
                return new NativeHelmResponse(Error: error);

            return response.ResultKind switch
            {
                ResultAck => new NativeHelmResponse(),
                ResultText => new NativeHelmResponse(Text: ReadString(response.Text) ?? string.Empty),
                ResultJson => new NativeHelmResponse(Values: ReadJsonElement(ReadString(response.Json) ?? "null")),
                ResultRelease => new NativeHelmResponse(Release: ReadRelease(response.Release)),
                ResultReleases => new NativeHelmResponse(Releases: ReadReleases(response.Releases)),
                ResultRepositories => new NativeHelmResponse(Repositories: ReadRepositories(response.Repositories)),
                _ => throw new InvalidOperationException($"The Helm native library returned unknown result kind {response.ResultKind}.")
            };
        }

        private static string? ReadString(nint value) => value == 0 ? null : Marshal.PtrToStringUTF8(value);

        private static JsonElement ReadJsonElement(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        private static NativeHelmRelease ReadRelease(nint value)
        {
            if (value == 0)
                throw new InvalidOperationException("The Helm native library returned no release.");
            var release = Marshal.PtrToStructure<NativeHelmReleaseAbi>(value);
            return new NativeHelmRelease
            {
                Name = ReadString(release.Name) ?? string.Empty,
                Namespace = ReadString(release.Namespace) ?? string.Empty,
                Revision = ReadString(release.Revision) ?? string.Empty,
                Updated = ReadString(release.Updated) ?? string.Empty,
                Status = ReadString(release.Status) ?? string.Empty,
                Chart = ReadString(release.Chart) ?? string.Empty,
                AppVersion = ReadString(release.AppVersion) ?? string.Empty
            };
        }

        private static NativeHelmRelease[] ReadReleases(NativeHelmReleaseArrayAbi array)
        {
            var length = ValidateArray(array.Items, array.Length, "release");
            var releases = new NativeHelmRelease[length];
            var itemSize = Marshal.SizeOf<NativeHelmReleaseAbi>();
            for (var index = 0; index < length; index++)
            {
                var item = Marshal.PtrToStructure<NativeHelmReleaseAbi>(array.Items + index * itemSize);
                releases[index] = new NativeHelmRelease
                {
                    Name = ReadString(item.Name) ?? string.Empty,
                    Namespace = ReadString(item.Namespace) ?? string.Empty,
                    Revision = ReadString(item.Revision) ?? string.Empty,
                    Updated = ReadString(item.Updated) ?? string.Empty,
                    Status = ReadString(item.Status) ?? string.Empty,
                    Chart = ReadString(item.Chart) ?? string.Empty,
                    AppVersion = ReadString(item.AppVersion) ?? string.Empty
                };
            }
            return releases;
        }

        private static HelmRepository[] ReadRepositories(NativeHelmRepositoryArrayAbi array)
        {
            var length = ValidateArray(array.Items, array.Length, "repository");
            var repositories = new HelmRepository[length];
            var itemSize = Marshal.SizeOf<NativeHelmRepositoryAbi>();
            for (var index = 0; index < length; index++)
            {
                var item = Marshal.PtrToStructure<NativeHelmRepositoryAbi>(array.Items + index * itemSize);
                repositories[index] = new HelmRepository(
                    ReadString(item.Name) ?? string.Empty,
                    ReadString(item.Url) ?? string.Empty);
            }
            return repositories;
        }

        private static int ValidateArray(nint items, nuint length, string itemName)
        {
            if (length > int.MaxValue || (length > 0 && items == 0))
                throw new InvalidOperationException($"The Helm native library returned an invalid {itemName} array.");
            return (int)length;
        }

        private static partial class NativeMethods
        {
            [LibraryImport("helmshim", EntryPoint = "HelmExecuteV1")]
            [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
            internal static partial nint HelmExecuteV1(nint input);

            [LibraryImport("helmshim", EntryPoint = "HelmFreeResponseV1")]
            [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
            internal static partial void HelmFreeResponseV1(nint response);
        }
    }

    private sealed class NativeRequestBuffer : IDisposable
    {
        private readonly List<nint> _allocations = [];

        internal nint AddString(string? value)
        {
            if (value is null)
                return 0;

            var bytes = Encoding.UTF8.GetBytes(value + '\0');
            var memory = Marshal.AllocHGlobal(bytes.Length);
            _allocations.Add(memory);
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            return memory;
        }

        internal nint AddStringArray(string[]? values)
        {
            if (values is null || values.Length == 0)
                return 0;

            var memory = Marshal.AllocHGlobal(checked(values.Length * IntPtr.Size));
            _allocations.Add(memory);
            for (var index = 0; index < values.Length; index++)
                Marshal.WriteIntPtr(memory, index * IntPtr.Size, AddString(values[index]));
            return memory;
        }

        public void Dispose()
        {
            for (var index = _allocations.Count - 1; index >= 0; index--)
                Marshal.FreeHGlobal(_allocations[index]);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeStringArrayAbi
    {
        internal nint Items;
        internal nuint Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmRequestAbi
    {
        internal uint AbiVersion;
        internal nint Operation;
        internal nint ReleaseName;
        internal nint Chart;
        internal NativeStringArrayAbi ValuesFiles;
        internal nint ValuesJson;
        internal int CreateNamespace;
        internal int Wait;
        internal long TimeoutMilliseconds;
        internal nint Kubeconfig;
        internal nint KubeContext;
        internal nint Namespace;
        internal int AllNamespaces;
        internal nint Filter;
        internal int Revision;
        internal int InstallIfMissing;
        internal nint RepositoryConfig;
        internal nint RepositoryCache;
        internal nint RegistryConfig;
        internal nint HelmDriver;
        internal nint RepositoryName;
        internal nint RepositoryUrl;
        internal nint RepositoryUsername;
        internal nint RepositoryPassword;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmReleaseAbi
    {
        internal nint Name;
        internal nint Namespace;
        internal nint Revision;
        internal nint Updated;
        internal nint Status;
        internal nint Chart;
        internal nint AppVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmReleaseArrayAbi
    {
        internal nint Items;
        internal nuint Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmRepositoryAbi
    {
        internal nint Name;
        internal nint Url;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmRepositoryArrayAbi
    {
        internal nint Items;
        internal nuint Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmResponseAbi
    {
        internal uint AbiVersion;
        internal int ResultKind;
        internal nint Error;
        internal nint Text;
        internal nint Json;
        internal nint Release;
        internal NativeHelmReleaseArrayAbi Releases;
        internal NativeHelmRepositoryArrayAbi Repositories;
    }
}
