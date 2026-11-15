using System.Text.Json;

namespace Helm.NET.Tests;

public sealed class HelmClientTests
{
    [Fact]
    public void ConstructorUsesDefaultOptions()
    {
        var client = new HelmClient();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void ListReleasesUsesDefaultsAndMapsTypedRecords()
    {
        var client = CreateClient(Response(releases: [NativeRelease()]), out var request);

        var releases = client.ListReleases();

        request().Operation.ShouldBe("list");
        request().Namespace.ShouldBe("default");
        request().AllNamespaces.ShouldBeFalse();
        request().Filter.ShouldBeNull();
        releases.ShouldHaveSingleItem().ShouldBe(Release());
    }

    [Fact]
    public void ListReleasesForwardsNamespaceAndFilterOptions()
    {
        var client = CreateClient(Response(), out var request);

        client.ListReleases(allNamespaces: true, filter: "^web$");

        request().AllNamespaces.ShouldBeTrue();
        request().Filter.ShouldBe("^web$");
    }

    [Fact]
    public void TemplateForwardsValuesFilesAndDynamicJsonValuesAndReturnsText()
    {
        using var valuesDocument = JsonDocument.Parse("""{"replicas":2,"configuration":{"nested":{"enabled":true,"items":[2,3]}}}""");
        var client = CreateClient(Response(text: "rendered yaml"), out var request);

        client.Template("web", "./chart", ["prod.yaml"], valuesDocument.RootElement).ShouldBe("rendered yaml");

        var captured = request();
        captured.Operation.ShouldBe("template");
        captured.ReleaseName.ShouldBe("web");
        captured.Chart.ShouldBe("./chart");
        captured.ValuesFiles.ShouldBe(["prod.yaml"]);
        var values = captured.Values!.Value;
        values.GetProperty("replicas").GetInt32().ShouldBe(2);
        var configuration = values.GetProperty("configuration");
        configuration.GetProperty("nested").GetProperty("enabled").GetBoolean().ShouldBeTrue();
        configuration.GetProperty("nested").GetProperty("items")[1].GetInt32().ShouldBe(3);
    }

    [Fact]
    public void TemplateDefaultsToNoValuesFilesOrValues()
    {
        var client = CreateClient(Response(text: "manifest"), out var request);

        client.Template("web", "chart").ShouldBe("manifest");

        request().ValuesFiles.ShouldBeNull();
        request().Values.ShouldBeNull();
    }

    [Fact]
    public void InstallForwardsOptionsAndMapsRelease()
    {
        var client = CreateClient(Response(release: NativeRelease()), out var request);
        using var valuesDocument = JsonDocument.Parse("""{"key":"value"}""");

        client.Install("web", "chart", ["values.yaml"], valuesDocument.RootElement, createNamespace: true, wait: true, timeout: TimeSpan.FromSeconds(12.5))
            .ShouldBe(Release());

        var captured = request();
        captured.Operation.ShouldBe("install");
        captured.CreateNamespace.ShouldBeTrue();
        captured.Wait.ShouldBeTrue();
        captured.TimeoutMilliseconds.ShouldBe(12500);
        captured.ValuesFiles.ShouldBe(["values.yaml"]);
        captured.Values!.Value.GetProperty("key").GetString().ShouldBe("value");
    }

    [Fact]
    public void UpgradeForwardsInstallIfMissingAndWaitOptions()
    {
        var client = CreateClient(Response(release: NativeRelease()), out var request);

        client.Upgrade("web", "chart", installIfMissing: true, createNamespace: true, wait: true, timeout: TimeSpan.FromSeconds(7))
            .ShouldBe(Release());

        var captured = request();
        captured.Operation.ShouldBe("upgrade");
        captured.InstallIfMissing.ShouldBeTrue();
        captured.CreateNamespace.ShouldBeTrue();
        captured.Wait.ShouldBeTrue();
        captured.TimeoutMilliseconds.ShouldBe(7000);
    }

    [Fact]
    public void InstallAndUpgradeUseFalseAndZeroOptionDefaults()
    {
        var client = CreateClient(Response(release: NativeRelease()), out var request);

        client.Install("web", "chart");
        request().CreateNamespace.ShouldBeFalse();
        request().Wait.ShouldBeFalse();
        request().TimeoutMilliseconds.ShouldBe(0);
        request().ValuesFiles.ShouldBeNull();
        request().Values.ShouldBeNull();

        client.Upgrade("web", "chart");
        request().InstallIfMissing.ShouldBeFalse();
        request().CreateNamespace.ShouldBeFalse();
        request().Wait.ShouldBeFalse();
        request().TimeoutMilliseconds.ShouldBe(0);
        request().ValuesFiles.ShouldBeNull();
        request().Values.ShouldBeNull();
    }

    [Fact]
    public void StatusMapsTypedRelease()
    {
        var client = CreateClient(Response(release: NativeRelease()), out var request);

        client.Status("web").ShouldBe(Release());

        request().ReleaseName.ShouldBe("web");
        request().Operation.ShouldBe("status");
    }

    [Fact]
    public void HistoryMapsTypedReleaseArray()
    {
        var previous = NativeRelease(revision: "2");
        var client = CreateClient(Response(releases: [NativeRelease(), previous]), out var request);

        client.History("web").ShouldBe([Release(), Release("2")]);

        request().Operation.ShouldBe("history");
        request().ReleaseName.ShouldBe("web");
    }

    [Fact]
    public void RollbackForwardsRevisionAndWait()
    {
        var client = CreateClient(Response(), out var request);

        client.Rollback("web", 2, wait: true);

        var captured = request();
        captured.Operation.ShouldBe("rollback");
        captured.ReleaseName.ShouldBe("web");
        captured.Revision.ShouldBe(2);
        captured.Wait.ShouldBeTrue();
    }

    [Fact]
    public void RollbackDefaultsToPreviousRevisionWithoutWaiting()
    {
        var client = CreateClient(Response(), out var request);

        client.Rollback("web");

        request().Revision.ShouldBe(0);
        request().Wait.ShouldBeFalse();
    }

    [Fact]
    public void RollbackWithRevisionDefaultsToNotWaiting()
    {
        var client = CreateClient(Response(), out var request);

        client.Rollback("web", 4);

        request().Revision.ShouldBe(4);
        request().Wait.ShouldBeFalse();
    }

    [Fact]
    public void UninstallForwardsWaitOption()
    {
        var client = CreateClient(Response(), out var request);

        client.Uninstall("web", wait: true);

        request().Operation.ShouldBe("uninstall");
        request().ReleaseName.ShouldBe("web");
        request().Wait.ShouldBeTrue();
    }

    [Fact]
    public void UninstallDefaultsToNotWaiting()
    {
        var client = CreateClient(Response(), out var request);

        client.Uninstall("web");

        request().Wait.ShouldBeFalse();
    }

    [Fact]
    public void GetValuesMapsDynamicJsonAndUsesConfiguredNamespace()
    {
        var client = CreateClient(Response(values: ParseJsonElement("""{"replicas":3,"enabled":true,"nested":{"zone":"west"}}""")), out var request,
            new HelmClientOptions { Namespace = "apps" });

        var values = client.GetValues("web");

        values.GetProperty("replicas").GetInt32().ShouldBe(3);
        values.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        values.GetProperty("nested").GetProperty("zone").GetString().ShouldBe("west");
        request().Namespace.ShouldBe("apps");
        request().Operation.ShouldBe("values");
    }

    [Fact]
    public void ListRepositoriesMapsTypedRepositoryArray()
    {
        var expected = new HelmRepository("stable", "https://example.test");
        var client = CreateClient(Response(repositories: [expected]), out var request);

        client.ListRepositories().ShouldHaveSingleItem().ShouldBe(expected);

        request().Operation.ShouldBe("repoList");
    }

    [Fact]
    public void AddRepositoryForwardsCredentials()
    {
        var client = CreateClient(Response(), out var request);

        client.AddRepository("private", "https://example.test", "user", "secret");

        var captured = request();
        captured.Operation.ShouldBe("repoAdd");
        captured.RepositoryName.ShouldBe("private");
        captured.RepositoryURL.ShouldBe("https://example.test");
        captured.RepositoryUsername.ShouldBe("user");
        captured.RepositoryPassword.ShouldBe("secret");
    }

    [Fact]
    public void AddRepositoryDefaultsCredentialsToNull()
    {
        var client = CreateClient(Response(), out var request);

        client.AddRepository("public", "https://example.test");

        request().RepositoryUsername.ShouldBeNull();
        request().RepositoryPassword.ShouldBeNull();
    }

    [Fact]
    public void RemoveRepositoryForwardsName()
    {
        var client = CreateClient(Response(), out var request);

        client.RemoveRepository("stable");

        request().RepositoryName.ShouldBe("stable");
        request().Operation.ShouldBe("repoRemove");
    }

    [Fact]
    public void UpdateRepositoriesDispatchesUpdateOperation()
    {
        var client = CreateClient(Response(), out var request);

        client.UpdateRepositories();

        request().Operation.ShouldBe("repoUpdate");
    }

    [Fact]
    public void InvokeMergesAllConfiguredOptionsIntoRequest()
    {
        var options = new HelmClientOptions
        {
            Kubeconfig = "kube.yaml", KubeContext = "dev", Namespace = "apps",
            RepositoryConfig = "repositories.yaml", RepositoryCache = "cache",
            RegistryConfig = "registry.json", HelmDriver = "configmap"
        };
        var client = CreateClient(Response(), out var request, options);

        client.ListReleases();

        var captured = request();
        captured.Kubeconfig.ShouldBe("kube.yaml");
        captured.KubeContext.ShouldBe("dev");
        captured.Namespace.ShouldBe("apps");
        captured.RepositoryConfig.ShouldBe("repositories.yaml");
        captured.RepositoryCache.ShouldBe("cache");
        captured.RegistryConfig.ShouldBe("registry.json");
        captured.HelmDriver.ShouldBe("configmap");
    }

    [Fact]
    public void InvokeThrowsActionExceptionWhenResponseHasError()
    {
        var client = CreateClient(Response(error: "release not found", release: NativeRelease()), out _);

        Should.Throw<HelmActionException>(() => client.Status("missing")).Message.ShouldBe("release not found");
    }

    [Fact]
    public void OperationsWithMissingOptionalResultsReturnExistingEmptyDefaults()
    {
        var client = CreateClient(Response(), out _);

        client.ListReleases().ShouldBeEmpty();
        client.History("web").ShouldBeEmpty();
        client.ListRepositories().ShouldBeEmpty();
        var values = client.GetValues("web");
        values.ValueKind.ShouldBe(JsonValueKind.Object);
        values.EnumerateObject().Any().ShouldBeFalse();
        client.Template("web", "chart").ShouldBeEmpty();
    }

    [Fact]
    public void InstallThrowsWhenResponseHasNoRelease()
    {
        var client = CreateClient(Response(), out _);

        Should.Throw<InvalidOperationException>(() => client.Install("web", "chart"));
    }

    [Fact]
    public void StatusThrowsWhenResponseHasNoRelease()
    {
        var client = CreateClient(Response(), out _);

        Should.Throw<InvalidOperationException>(() => client.Status("web"));
    }

    private static HelmClient CreateClient(
        NativeHelmResponse response,
        out Func<NativeHelmRequest> readRequest,
        HelmClientOptions? options = null)
    {
        NativeHelmRequest? captured = null;
        var nativeApi = new FakeNativeApi(request =>
        {
            captured = request;
            return response;
        });
        var client = new HelmClient(options ?? new HelmClientOptions(), nativeApi);
        readRequest = () => captured ?? throw new InvalidOperationException("No native request was captured.");
        return client;
    }

    private sealed class FakeNativeApi(Func<NativeHelmRequest, NativeHelmResponse> execute) : IHelmNativeApi
    {
        public NativeHelmResponse Execute(NativeHelmRequest request) => execute(request);
    }

    private static NativeHelmResponse Response(
        string? error = null,
        string? text = null,
        JsonElement? values = null,
        NativeHelmRelease? release = null,
        NativeHelmRelease[]? releases = null,
        HelmRepository[]? repositories = null) => new()
        {
            Error = error,
            Text = text,
            Values = values,
            Release = release,
            Releases = releases,
            Repositories = repositories
        };

    private static JsonElement ParseJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static NativeHelmRelease NativeRelease(string revision = "3") => new()
    {
        Name = "web",
        Namespace = "apps",
        Revision = revision,
        Updated = "2026-01-02T03:04:05Z",
        Status = "deployed",
        Chart = "nginx-1.2.3",
        AppVersion = "2.0"
    };

    private static HelmRelease Release(string revision = "3") => new(
        "web", "apps", revision, "2026-01-02T03:04:05Z", "deployed", "nginx-1.2.3", "2.0");
}

