using System.Globalization;
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
    private const int ResultChartSearchResults = 7;

    [Flags]
    private enum NativeRequestFlags : ulong
    {
        AllValues = 1UL << 0,
        Versions = 1UL << 1,
        Devel = 1UL << 2,
        Regexp = 1UL << 3,
        FailOnNoResult = 1UL << 4,
        Deployed = 1UL << 5,
        Failed = 1UL << 6,
        Pending = 1UL << 7,
        Superseded = 1UL << 8,
        Uninstalled = 1UL << 9,
        Uninstalling = 1UL << 10,
        Date = 1UL << 11,
        ResetValues = 1UL << 12,
        ReuseValues = 1UL << 13,
        ResetThenReuseValues = 1UL << 14,
        HideSecret = 1UL << 15,
        DisableHooks = 1UL << 16,
        WaitForJobs = 1UL << 17,
        RollbackOnFailure = 1UL << 18,
        Reverse = 1UL << 19,
        DependencyUpdate = 1UL << 20,
        SkipCrds = 1UL << 21,
        SkipSchemaValidation = 1UL << 22,
        DisableOpenApiValidation = 1UL << 23,
        ForceUpdate = 1UL << 24,
        Replace = 1UL << 25,
        ForceReplace = 1UL << 26,
        ForceConflicts = 1UL << 27,
        TakeOwnership = 1UL << 28,
        CleanupOnFail = 1UL << 29,
        DryRunUninstall = 1UL << 30,
        IgnoreNotFound = 1UL << 31,
        KeepHistory = 1UL << 32,
        InsecureSkipTlsVerify = 1UL << 33,
        PassCredentials = 1UL << 34,
        PlainHttp = 1UL << 35,
        IncludeCrds = 1UL << 36,
        SkipTests = 1UL << 37,
        IsUpgrade = 1UL << 38
    }

    private static ulong BuildRequestFlags(NativeHelmRequest request)
    {
        NativeRequestFlags flags = 0;
        Add(request.AllValues, NativeRequestFlags.AllValues);
        Add(request.Versions, NativeRequestFlags.Versions);
        Add(request.Devel, NativeRequestFlags.Devel);
        Add(request.Regexp, NativeRequestFlags.Regexp);
        Add(request.FailOnNoResult, NativeRequestFlags.FailOnNoResult);
        Add(request.Deployed, NativeRequestFlags.Deployed);
        Add(request.Failed, NativeRequestFlags.Failed);
        Add(request.Pending, NativeRequestFlags.Pending);
        Add(request.Superseded, NativeRequestFlags.Superseded);
        Add(request.Uninstalled, NativeRequestFlags.Uninstalled);
        Add(request.Uninstalling, NativeRequestFlags.Uninstalling);
        Add(request.Date, NativeRequestFlags.Date);
        Add(request.ResetValues, NativeRequestFlags.ResetValues);
        Add(request.ReuseValues, NativeRequestFlags.ReuseValues);
        Add(request.ResetThenReuseValues, NativeRequestFlags.ResetThenReuseValues);
        Add(request.HideSecret, NativeRequestFlags.HideSecret);
        Add(request.DisableHooks, NativeRequestFlags.DisableHooks);
        Add(request.WaitForJobs, NativeRequestFlags.WaitForJobs);
        Add(request.RollbackOnFailure, NativeRequestFlags.RollbackOnFailure);
        Add(request.Reverse, NativeRequestFlags.Reverse);
        Add(request.DependencyUpdate, NativeRequestFlags.DependencyUpdate);
        Add(request.SkipCrds, NativeRequestFlags.SkipCrds);
        Add(request.SkipSchemaValidation, NativeRequestFlags.SkipSchemaValidation);
        Add(request.DisableOpenApiValidation, NativeRequestFlags.DisableOpenApiValidation);
        Add(request.ForceUpdate, NativeRequestFlags.ForceUpdate);
        Add(request.Replace, NativeRequestFlags.Replace);
        Add(request.ForceReplace, NativeRequestFlags.ForceReplace);
        Add(request.ForceConflicts, NativeRequestFlags.ForceConflicts);
        Add(request.TakeOwnership, NativeRequestFlags.TakeOwnership);
        Add(request.CleanupOnFail, NativeRequestFlags.CleanupOnFail);
        Add(request.DryRunUninstall, NativeRequestFlags.DryRunUninstall);
        Add(request.IgnoreNotFound, NativeRequestFlags.IgnoreNotFound);
        Add(request.KeepHistory, NativeRequestFlags.KeepHistory);
        Add(request.InsecureSkipTlsVerify, NativeRequestFlags.InsecureSkipTlsVerify);
        Add(request.PassCredentials, NativeRequestFlags.PassCredentials);
        Add(request.PlainHttp, NativeRequestFlags.PlainHttp);
        Add(request.IncludeCrds, NativeRequestFlags.IncludeCrds);
        Add(request.SkipTests, NativeRequestFlags.SkipTests);
        Add(request.IsUpgrade, NativeRequestFlags.IsUpgrade);
        return (ulong)flags;

        void Add(bool enabled, NativeRequestFlags flag)
        {
            if (enabled)
                flags |= flag;
        }
    }

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
                ? values.GetRawText()
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
                RepositoryPassword = input.AddString(request.RepositoryPassword),
                Options = new NativeHelmRequestOptionsAbi
                {
                    Version = input.AddString(request.Version),
                    SearchTerm = input.AddString(request.SearchTerm),
                    VersionConstraint = input.AddString(request.VersionConstraint),
                    Selector = input.AddString(request.Selector),
                    DryRun = input.AddString(request.DryRun),
                    PostRenderer = input.AddString(request.PostRenderer),
                    Description = input.AddString(request.Description),
                    WaitStrategy = input.AddString(request.WaitStrategy),
                    Cascade = input.AddString(request.Cascade),
                    Repo = input.AddString(request.Repo),
                    CaFile = input.AddString(request.CaFile),
                    CertFile = input.AddString(request.CertFile),
                    KeyFile = input.AddString(request.KeyFile),
                    KubeVersion = input.AddString(request.KubeVersion),
                    PostRendererArgs = new NativeStringArrayAbi
                    {
                        Items = input.AddStringArray(request.PostRendererArgs),
                        Length = (nuint)(request.PostRendererArgs?.Length ?? 0)
                    },
                    ApiVersions = new NativeStringArrayAbi
                    {
                        Items = input.AddStringArray(request.ApiVersions),
                        Length = (nuint)(request.ApiVersions?.Length ?? 0)
                    },
                    Max = request.Max,
                    Offset = request.Offset,
                    HistoryMax = request.HistoryMax,
                    Flags = BuildRequestFlags(request)
                }
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
                ResultChartSearchResults => new NativeHelmResponse(ChartSearchResults: ReadChartSearchResults(response.ChartSearchResults)),
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
                Revision = release.Revision,
                Updated = ReadTimestamp(release.Updated),
                Status = ReadString(release.Status) ?? string.Empty,
                Chart = ReadString(release.Chart) ?? string.Empty,
                AppVersion = ReadString(release.AppVersion) ?? string.Empty,
                ChartName = ReadString(release.ChartName) ?? string.Empty,
                ChartVersion = ReadString(release.ChartVersion) ?? string.Empty,
                Description = ReadString(release.Description) ?? string.Empty,
                Notes = ReadString(release.Notes) ?? string.Empty,
                Resources = ReadOptionalJsonElement(ReadString(release.ResourcesJson)),
                Hooks = ReadHooks(release.Hooks)
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
                    Revision = item.Revision,
                    Updated = ReadTimestamp(item.Updated),
                    Status = ReadString(item.Status) ?? string.Empty,
                    Chart = ReadString(item.Chart) ?? string.Empty,
                    AppVersion = ReadString(item.AppVersion) ?? string.Empty,
                    ChartName = ReadString(item.ChartName) ?? string.Empty,
                    ChartVersion = ReadString(item.ChartVersion) ?? string.Empty,
                    Description = ReadString(item.Description) ?? string.Empty,
                    Notes = ReadString(item.Notes) ?? string.Empty,
                    Resources = ReadOptionalJsonElement(ReadString(item.ResourcesJson)),
                    Hooks = ReadHooks(item.Hooks)
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

        private static NativeHelmHook[] ReadHooks(NativeHelmReleaseHookArrayAbi array)
        {
            var length = ValidateArray(array.Items, array.Length, "release hook");
            var hooks = new NativeHelmHook[length];
            var itemSize = Marshal.SizeOf<NativeHelmReleaseHookAbi>();
            for (var index = 0; index < length; index++)
            {
                var item = Marshal.PtrToStructure<NativeHelmReleaseHookAbi>(array.Items + index * itemSize);
                hooks[index] = new NativeHelmHook
                {
                    Name = ReadString(item.Name) ?? string.Empty,
                    Kind = ReadString(item.Kind) ?? string.Empty,
                    Path = ReadString(item.Path) ?? string.Empty,
                    Manifest = ReadString(item.Manifest) ?? string.Empty,
                    Events = ReadStringArray(item.Events, "hook event"),
                    LastRun = new NativeHelmHookExecution
                    {
                        StartedAt = ReadOptionalTimestamp(item.LastRun.StartedAt),
                        CompletedAt = ReadOptionalTimestamp(item.LastRun.CompletedAt),
                        Phase = ReadString(item.LastRun.Phase) ?? string.Empty
                    },
                    Weight = item.Weight,
                    DeletePolicies = ReadStringArray(item.DeletePolicies, "hook delete policy")
                };
            }
            return hooks;
        }

        private static string[] ReadStringArray(NativeStringArrayAbi array, string itemName)
        {
            var length = ValidateArray(array.Items, array.Length, itemName);
            var values = new string[length];
            for (var index = 0; index < length; index++)
            {
                var value = Marshal.ReadIntPtr(array.Items, index * IntPtr.Size);
                values[index] = ReadString(value) ?? string.Empty;
            }
            return values;
        }

        private static HelmChartSearchResult[] ReadChartSearchResults(NativeHelmChartSearchResultArrayAbi array)
        {
            var length = ValidateArray(array.Items, array.Length, "chart search result");
            var results = new HelmChartSearchResult[length];
            var itemSize = Marshal.SizeOf<NativeHelmChartSearchResultAbi>();
            for (var index = 0; index < length; index++)
            {
                var item = Marshal.PtrToStructure<NativeHelmChartSearchResultAbi>(array.Items + index * itemSize);
                results[index] = new HelmChartSearchResult(
                    ReadString(item.Reference) ?? string.Empty,
                    ReadString(item.ChartName) ?? string.Empty,
                    ReadString(item.Version) ?? string.Empty,
                    ReadString(item.Description) ?? string.Empty,
                    ReadString(item.AppVersion) ?? string.Empty);
            }
            return results;
        }

        private static DateTimeOffset ReadTimestamp(nint value)
        {
            var timestamp = ReadString(value);
            return string.IsNullOrEmpty(timestamp)
                ? DateTimeOffset.MinValue
                : DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private static DateTimeOffset? ReadOptionalTimestamp(nint value)
        {
            var timestamp = ReadString(value);
            return string.IsNullOrEmpty(timestamp)
                ? null
                : DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private static JsonElement? ReadOptionalJsonElement(string? json) =>
            string.IsNullOrEmpty(json) ? null : ReadJsonElement(json);

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
        internal NativeHelmRequestOptionsAbi Options;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmRequestOptionsAbi
    {
        internal nint Version;
        internal nint SearchTerm;
        internal nint VersionConstraint;
        internal nint Selector;
        internal nint DryRun;
        internal nint PostRenderer;
        internal nint Description;
        internal nint WaitStrategy;
        internal nint Cascade;
        internal nint Repo;
        internal nint CaFile;
        internal nint CertFile;
        internal nint KeyFile;
        internal nint KubeVersion;
        internal NativeStringArrayAbi PostRendererArgs;
        internal NativeStringArrayAbi ApiVersions;
        internal int Max;
        internal int Offset;
        internal int HistoryMax;
        internal ulong Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmReleaseAbi
    {
        internal nint Name;
        internal nint Namespace;
        internal int Revision;
        internal nint Updated;
        internal nint Status;
        internal nint Chart;
        internal nint AppVersion;
        internal nint ChartName;
        internal nint ChartVersion;
        internal nint Description;
        internal nint Notes;
        internal nint ResourcesJson;
        internal NativeHelmReleaseHookArrayAbi Hooks;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmReleaseHookAbi
    {
        internal nint Name;
        internal nint Kind;
        internal nint Path;
        internal nint Manifest;
        internal NativeStringArrayAbi Events;
        internal NativeHelmHookExecutionAbi LastRun;
        internal int Weight;
        internal NativeStringArrayAbi DeletePolicies;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmHookExecutionAbi
    {
        internal nint StartedAt;
        internal nint CompletedAt;
        internal nint Phase;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmReleaseHookArrayAbi
    {
        internal nint Items;
        internal nuint Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmChartSearchResultAbi
    {
        internal nint Reference;
        internal nint ChartName;
        internal nint Version;
        internal nint AppVersion;
        internal nint Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHelmChartSearchResultArrayAbi
    {
        internal nint Items;
        internal nuint Length;
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
        internal NativeHelmChartSearchResultArrayAbi ChartSearchResults;
    }
}
