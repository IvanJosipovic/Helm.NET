using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Helm.NET.Tests;

public sealed class HelmNativeInteropTests
{
    [Fact]
    public void ExecuteMarshalsUtf8RequestAndFreesResponseOnce()
    {
        using var memory = new NativeMemoryFixture();
        var response = CreateResponse(memory, HelmNativeInterop.ResultText, text: "rendered");
        var responsePointer = memory.Add(response);
        var freeCount = 0;
        using var valuesDocument = JsonDocument.Parse("""{"雪":"café"}""");
        var api = new HelmNativeInterop.NativeHelmApi(
            requestPointer =>
            {
                var request = Marshal.PtrToStructure<HelmNativeInterop.NativeHelmRequestAbi>(requestPointer);
                request.AbiVersion.ShouldBe(HelmNativeInterop.AbiVersion);
                NativeMemoryFixture.ReadString(request.Operation).ShouldBe("install");
                NativeMemoryFixture.ReadString(request.ReleaseName).ShouldBe("web-雪");
                NativeMemoryFixture.ReadString(request.Chart).ShouldBe("./charts/雪");
                NativeMemoryFixture.ReadString(request.Namespace).ShouldBe("apps");
                NativeMemoryFixture.ReadString(request.KubeContext).ShouldBeNull();
                NativeMemoryFixture.ReadString(request.ValuesJson).ShouldBe("{\"雪\":\"café\"}");
                request.CreateNamespace.ShouldBe(1);
                request.Wait.ShouldBe(1);
                request.TimeoutMilliseconds.ShouldBe(12345);
                request.AllNamespaces.ShouldBe(1);

                NativeMemoryFixture.ReadStringArray(request.ValuesFiles).ShouldBe(["prod-雪.yaml", "overrides.yaml"]);
                NativeMemoryFixture.ReadString(request.Options.Version).ShouldBe("1.2.3");
                NativeMemoryFixture.ReadStringArray(request.Options.ApiVersions).ShouldBe(["v1", "apps/v1"]);
                NativeMemoryFixture.ReadStringArray(request.Options.PostRendererArgs).ShouldBe(["--flag"]);
                request.Options.Max.ShouldBe(17);
                request.Options.HistoryMax.ShouldBe(10);
                request.Options.Flags.ShouldBe((1UL << 15) | (1UL << 36) | (1UL << 38));
                request.RepositoryPassword.ShouldBe(nint.Zero);
                return responsePointer;
            },
            pointer =>
            {
                pointer.ShouldBe(responsePointer);
                freeCount++;
                memory.Dispose();
            });

        var result = api.Execute(new NativeHelmRequest(
            "install",
            ReleaseName: "web-雪",
            Chart: "./charts/雪",
            ValuesFiles: ["prod-雪.yaml", "overrides.yaml"],
            Values: valuesDocument.RootElement.Clone(),
            CreateNamespace: true,
            Wait: true,
            TimeoutMilliseconds: 12345,
            Namespace: "apps",
            AllNamespaces: true,
            Version: "1.2.3",
            Max: 17,
            HistoryMax: 10,
            ApiVersions: ["v1", "apps/v1"],
            PostRendererArgs: ["--flag"],
            HideSecret: true,
            IncludeCrds: true,
            IsUpgrade: true));

        result.Text.ShouldBe("rendered");
        freeCount.ShouldBe(1);
    }

    [Fact]
    public void ExecuteDecodesAcknowledgementTextAndDetachedJson()
    {
        using (var memory = new NativeMemoryFixture())
        {
            var api = CreateApi(memory, CreateResponse(memory, HelmNativeInterop.ResultAck));
            api.Execute(new NativeHelmRequest("repoUpdate")).ShouldBe(new NativeHelmResponse());
        }

        JsonElement values;
        using (var memory = new NativeMemoryFixture())
        {
            var api = CreateApi(memory, CreateResponse(memory, HelmNativeInterop.ResultJson, json: "{\"nested\":{\"enabled\":true}}"));
            values = api.Execute(new NativeHelmRequest("values")).Values!.Value;
        }

        values.GetProperty("nested").GetProperty("enabled").GetBoolean().ShouldBeTrue();

        using var textMemory = new NativeMemoryFixture();
        var textApi = CreateApi(textMemory, CreateResponse(textMemory, HelmNativeInterop.ResultText));
        textApi.Execute(new NativeHelmRequest("template")).Text.ShouldBe(string.Empty);
    }

    [Fact]
    public void ExecuteDecodesReleaseHooksAndReleaseArrays()
    {
        using (var memory = new NativeMemoryFixture())
        {
            var hook = new HelmNativeInterop.NativeHelmReleaseHookAbi
            {
                Name = memory.AddString("smoke"),
                Kind = memory.AddString("Pod"),
                Path = memory.AddString("templates/hook.yaml"),
                Manifest = memory.AddString("kind: Pod"),
                Events = memory.AddStringArray(["test"]),
                LastRun = new HelmNativeInterop.NativeHelmHookExecutionAbi
                {
                    StartedAt = memory.AddString("2026-01-02T03:04:05Z"),
                    CompletedAt = memory.AddString("2026-01-02T03:04:06Z"),
                    Phase = memory.AddString("Succeeded")
                },
                Weight = 4,
                DeletePolicies = memory.AddStringArray(["before-hook-creation"])
            };
            var hookWithoutRunData = new HelmNativeInterop.NativeHelmReleaseHookAbi
            {
                Name = memory.AddString("pending")
            };
            var hookPointer = memory.AddArray([hook, hookWithoutRunData]);
            var releasePointer = memory.Add(new HelmNativeInterop.NativeHelmReleaseAbi
            {
                Name = memory.AddString("web"),
                Namespace = memory.AddString("apps"),
                Revision = 3,
                Updated = memory.AddString("2026-01-02T03:04:05.1234567+02:00"),
                Status = memory.AddString("deployed"),
                Chart = memory.AddString("nginx-1.2.3"),
                AppVersion = memory.AddString("2.0"),
                ChartName = memory.AddString("nginx"),
                ChartVersion = memory.AddString("1.2.3"),
                Description = memory.AddString("release description"),
                Notes = memory.AddString("release notes"),
                ResourcesJson = memory.AddString("{\"pods\":[\"web\"]}"),
                Hooks = new HelmNativeInterop.NativeHelmReleaseHookArrayAbi { Items = hookPointer, Length = 2 }
            });
            var api = CreateApi(memory, CreateResponse(memory, HelmNativeInterop.ResultRelease, release: releasePointer));

            var release = api.Execute(new NativeHelmRequest("status")).Release!;

            release.Name.ShouldBe("web");
            release.Namespace.ShouldBe("apps");
            release.Revision.ShouldBe(3);
            release.Updated.ShouldBe(DateTimeOffset.Parse("2026-01-02T03:04:05.1234567+02:00", CultureInfo.InvariantCulture));
            release.Chart.ShouldBe("nginx-1.2.3");
            release.Resources!.Value.GetProperty("pods")[0].GetString().ShouldBe("web");
            var parsedHook = release.Hooks[0];
            parsedHook.Name.ShouldBe("smoke");
            parsedHook.Events.ShouldBe(["test"]);
            parsedHook.LastRun.StartedAt.ShouldBe(DateTimeOffset.Parse("2026-01-02T03:04:05Z", CultureInfo.InvariantCulture));
            parsedHook.LastRun.CompletedAt.ShouldBe(DateTimeOffset.Parse("2026-01-02T03:04:06Z", CultureInfo.InvariantCulture));
            parsedHook.LastRun.Phase.ShouldBe("Succeeded");
            parsedHook.DeletePolicies.ShouldBe(["before-hook-creation"]);
            var pendingHook = release.Hooks[1];
            pendingHook.Name.ShouldBe("pending");
            pendingHook.LastRun.StartedAt.ShouldBeNull();
            pendingHook.LastRun.CompletedAt.ShouldBeNull();
        }

        using var releasesMemory = new NativeMemoryFixture();
        var releaseArray = releasesMemory.AddArray([new HelmNativeInterop.NativeHelmReleaseAbi
        {
            Name = releasesMemory.AddString("web"),
            Namespace = releasesMemory.AddString("apps"),
            Updated = releasesMemory.AddString(""),
            Hooks = new HelmNativeInterop.NativeHelmReleaseHookArrayAbi()
        }]);
        var releasesApi = CreateApi(releasesMemory, CreateResponse(
            releasesMemory,
            HelmNativeInterop.ResultReleases,
            releases: new HelmNativeInterop.NativeHelmReleaseArrayAbi { Items = releaseArray, Length = 1 }));

        var releases = releasesApi.Execute(new NativeHelmRequest("list")).Releases!;

        releases.ShouldHaveSingleItem().Name.ShouldBe("web");
        releases[0].Updated.ShouldBe(DateTimeOffset.MinValue);
    }

    [Fact]
    public void ExecuteDecodesRepositoryAndChartSearchArrays()
    {
        using (var memory = new NativeMemoryFixture())
        {
            var items = memory.AddArray([
                new HelmNativeInterop.NativeHelmRepositoryAbi
                {
                    Name = memory.AddString("stable"),
                    Url = memory.AddString("https://charts.example")
                }
            ]);
            var api = CreateApi(memory, CreateResponse(
                memory,
                HelmNativeInterop.ResultRepositories,
                repositories: new HelmNativeInterop.NativeHelmRepositoryArrayAbi { Items = items, Length = 1 }));

            api.Execute(new NativeHelmRequest("repoList")).Repositories.ShouldBe([new HelmRepository("stable", "https://charts.example")]);
        }

        using var searchMemory = new NativeMemoryFixture();
        var searchItems = searchMemory.AddArray([
            new HelmNativeInterop.NativeHelmChartSearchResultAbi
            {
                Reference = searchMemory.AddString("stable/nginx"),
                ChartName = searchMemory.AddString("nginx"),
                Version = searchMemory.AddString("1.2.3"),
                AppVersion = searchMemory.AddString("2.0"),
                Description = searchMemory.AddString("Nginx chart")
            }
        ]);
        var searchApi = CreateApi(searchMemory, CreateResponse(
            searchMemory,
            HelmNativeInterop.ResultChartSearchResults,
            chartSearchResults: new HelmNativeInterop.NativeHelmChartSearchResultArrayAbi { Items = searchItems, Length = 1 }));

        searchApi.Execute(new NativeHelmRequest("chartSearch")).ChartSearchResults.ShouldBe(
            [new HelmChartSearchResult("stable/nginx", "nginx", "1.2.3", "Nginx chart", "2.0")]);
    }

    [Fact]
    public void ExecuteReturnsNativeErrorsAndFreesResponse()
    {
        using var memory = new NativeMemoryFixture();
        var errorResponse = CreateResponse(memory, HelmNativeInterop.ResultAck, error: "release not found");
        var freeCount = 0;
        var api = CreateApi(memory, errorResponse, () => freeCount++);

        api.Execute(new NativeHelmRequest("status")).Error.ShouldBe("release not found");
        freeCount.ShouldBe(1);
    }

    [Fact]
    public void ExecuteRejectsNullResponseWithoutAttemptingToFreeIt()
    {
        var freeCount = 0;
        var api = new HelmNativeInterop.NativeHelmApi(_ => nint.Zero, _ => freeCount++);

        Should.Throw<InvalidOperationException>(() => api.Execute(new NativeHelmRequest("list")))
            .Message.ShouldBe("The Helm native library returned a null response.");
        freeCount.ShouldBe(0);
    }

    [Fact]
    public void ExecuteFreesMalformedNativeResponsesWhenDecodingFails()
    {
        var cases = new (string Name, Func<NativeMemoryFixture, HelmNativeInterop.NativeHelmResponseAbi> Response, Type ErrorType)[]
        {
            ("abi version", _ => new HelmNativeInterop.NativeHelmResponseAbi { AbiVersion = 2 }, typeof(InvalidOperationException)),
            ("unknown result kind", memory => CreateResponse(memory, 99), typeof(InvalidOperationException)),
            ("malformed json", memory => CreateResponse(memory, HelmNativeInterop.ResultJson, json: "{"), typeof(JsonException)),
            ("missing release", memory => CreateResponse(memory, HelmNativeInterop.ResultRelease), typeof(InvalidOperationException)),
            ("invalid release array", memory => CreateResponse(memory, HelmNativeInterop.ResultReleases,
                releases: new HelmNativeInterop.NativeHelmReleaseArrayAbi { Length = 1 }), typeof(InvalidOperationException)),
            ("oversized repository array", memory => CreateResponse(memory, HelmNativeInterop.ResultRepositories,
                repositories: new HelmNativeInterop.NativeHelmRepositoryArrayAbi { Length = nuint.MaxValue }), typeof(InvalidOperationException))
        };

        foreach (var test in cases)
        {
            using var memory = new NativeMemoryFixture();
            var freeCount = 0;
            var api = CreateApi(memory, test.Response(memory), () => freeCount++);

            var exception = Should.Throw<Exception>(() => api.Execute(new NativeHelmRequest("test")), test.Name);
            test.ErrorType.IsInstanceOfType(exception).ShouldBeTrue(test.Name);
            freeCount.ShouldBe(1, test.Name);
        }

        using var invalidTimestampMemory = new NativeMemoryFixture();
        var invalidRelease = invalidTimestampMemory.Add(new HelmNativeInterop.NativeHelmReleaseAbi
        {
            Updated = invalidTimestampMemory.AddString("not-a-timestamp")
        });
        var invalidTimestampApi = CreateApi(invalidTimestampMemory, CreateResponse(
            invalidTimestampMemory,
            HelmNativeInterop.ResultRelease,
            release: invalidRelease));
        Should.Throw<FormatException>(() => invalidTimestampApi.Execute(new NativeHelmRequest("status")));
    }

    [Fact]
    public void EmptyNativeArraysDecodeAsEmptyManagedArrays()
    {
        using (var memory = new NativeMemoryFixture())
        {
            var api = CreateApi(memory, CreateResponse(memory, HelmNativeInterop.ResultReleases));
            api.Execute(new NativeHelmRequest("list")).Releases.ShouldBeEmpty();
        }

        using (var memory = new NativeMemoryFixture())
        {
            var api = CreateApi(memory, CreateResponse(memory, HelmNativeInterop.ResultRepositories));
            api.Execute(new NativeHelmRequest("repoList")).Repositories.ShouldBeEmpty();
        }

        using var searchMemory = new NativeMemoryFixture();
        var searchApi = CreateApi(searchMemory, CreateResponse(searchMemory, HelmNativeInterop.ResultChartSearchResults));
        searchApi.Execute(new NativeHelmRequest("chartSearch")).ChartSearchResults.ShouldBeEmpty();
    }

    private static HelmNativeInterop.NativeHelmApi CreateApi(
        NativeMemoryFixture memory,
        HelmNativeInterop.NativeHelmResponseAbi response,
        Action? onFree = null)
    {
        var responsePointer = memory.Add(response);
        return new HelmNativeInterop.NativeHelmApi(
            _ => responsePointer,
            pointer =>
            {
                pointer.ShouldBe(responsePointer);
                onFree?.Invoke();
                memory.Dispose();
            });
    }

    private static HelmNativeInterop.NativeHelmResponseAbi CreateResponse(
        NativeMemoryFixture memory,
        int resultKind,
        string? error = null,
        string? text = null,
        string? json = null,
        nint release = default,
        HelmNativeInterop.NativeHelmReleaseArrayAbi releases = default,
        HelmNativeInterop.NativeHelmRepositoryArrayAbi repositories = default,
        HelmNativeInterop.NativeHelmChartSearchResultArrayAbi chartSearchResults = default) => new()
        {
            AbiVersion = HelmNativeInterop.AbiVersion,
            ResultKind = resultKind,
            Error = memory.AddString(error),
            Text = memory.AddString(text),
            Json = memory.AddString(json),
            Release = release,
            Releases = releases,
            Repositories = repositories,
            ChartSearchResults = chartSearchResults
        };

    private sealed class NativeMemoryFixture : IDisposable
    {
        private readonly List<nint> _allocations = [];

        public nint AddString(string? value)
        {
            if (value is null)
                return nint.Zero;

            var bytes = Encoding.UTF8.GetBytes(value + '\0');
            var pointer = Marshal.AllocHGlobal(bytes.Length);
            _allocations.Add(pointer);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return pointer;
        }

        public nint Add<T>(T value) where T : struct
        {
            var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
            _allocations.Add(pointer);
            Marshal.StructureToPtr(value, pointer, false);
            return pointer;
        }

        public nint AddArray<T>(T[] values) where T : struct
        {
            if (values.Length == 0)
                return nint.Zero;

            var itemSize = Marshal.SizeOf<T>();
            var pointer = Marshal.AllocHGlobal(checked(itemSize * values.Length));
            _allocations.Add(pointer);
            for (var index = 0; index < values.Length; index++)
                Marshal.StructureToPtr(values[index], pointer + (index * itemSize), false);
            return pointer;
        }

        public HelmNativeInterop.NativeStringArrayAbi AddStringArray(string[] values)
        {
            if (values.Length == 0)
                return new HelmNativeInterop.NativeStringArrayAbi();

            var pointer = Marshal.AllocHGlobal(checked(values.Length * IntPtr.Size));
            _allocations.Add(pointer);
            for (var index = 0; index < values.Length; index++)
                Marshal.WriteIntPtr(pointer, index * IntPtr.Size, AddString(values[index]));
            return new HelmNativeInterop.NativeStringArrayAbi { Items = pointer, Length = (nuint)values.Length };
        }

        public static string? ReadString(nint pointer) => pointer == nint.Zero ? null : Marshal.PtrToStringUTF8(pointer);

        public static string[] ReadStringArray(HelmNativeInterop.NativeStringArrayAbi array)
        {
            var values = new string[(int)array.Length];
            for (var index = 0; index < values.Length; index++)
                values[index] = ReadString(Marshal.ReadIntPtr(array.Items, index * IntPtr.Size)) ?? string.Empty;
            return values;
        }

        public void Dispose()
        {
            for (var index = _allocations.Count - 1; index >= 0; index--)
                Marshal.FreeHGlobal(_allocations[index]);
            _allocations.Clear();
        }
    }
}
