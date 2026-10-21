# Helm.NET

[![Nuget](https://img.shields.io/nuget/vpre/Helm.NET.svg?style=flat-square)](https://www.nuget.org/packages/Helm.NET)
[![Nuget](https://img.shields.io/nuget/dt/Helm.NET.svg?style=flat-square)](https://www.nuget.org/packages/Helm.NET)
[![codecov](https://codecov.io/gh/IvanJosipovic/Helm.NET/graph/badge.svg?token=nZGzjHvaDh)](https://codecov.io/gh/IvanJosipovic/Helm.NET)

## What is this?

Helm.NET provides a .NET client backed by Helm's Go action library. The managed API calls a native library through a small C ABI shim and does not launch the Helm command-line executable.

## Supported platforms

Helm.NET includes native libraries for Windows, Linux, and macOS on both x64 and ARM64:

| Operating system | x64 | ARM64 |
| --- | --- | --- |
| Windows | `win-x64` | `win-arm64` |
| Linux | `linux-x64` | `linux-arm64` |
| macOS | `osx-x64` | `osx-arm64` |

### Install a chart

```csharp
using Helm.NET;

var helm = new HelmClient(new HelmClientOptions { Namespace = "apps" });

helm.AddRepository("bitnami", "https://charts.bitnami.com/bitnami");

helm.UpdateRepositories();

var release = helm.Install("my-release", "bitnami/nginx", createNamespace: true, wait: true);

// Search indexes return resolvable references and one result per version.
var charts = helm.SearchCharts("cilium");
// ListCharts reads the cached indexes without requiring a search term or refreshing them.
var bitnamiCharts = helm.ListCharts("bitnami");
var rendered = helm.Template("preview", "cilium/cilium", version: "1.20.3");

// Render the chart and values from a stored release revision without resolving a repository reference.
var installedChart = helm.TemplateRelease("my-release");

// Retrieve the manifest saved by Helm for the deployed revision.
var deployedManifest = helm.GetManifest("my-release");
```

### List releases

```csharp
IReadOnlyList<HelmRelease> releases = helm.ListReleases();
foreach (var release in releases)
    Console.WriteLine($"{release.Name}: {release.Status} ({release.Chart})");
```

The action API supports `ListReleases`, `ListRepositories`, `ListCharts`, `SearchCharts`, `Template`, `TemplateRelease`, `GetManifest`, `Install`, `Upgrade`, `Uninstall`, `Status`, `History`, `Rollback`, `GetValues`, and chart repository operations. `HelmRelease.ChartName` and `ChartVersion` expose the chart metadata separately from the display string in `Chart`. Configure kubeconfig, context, namespace, repository paths, registry config, and the release storage driver with `HelmClientOptions`.

### List repository charts

`ListCharts()` returns one result per chart version across configured Helm chart repositories. Pass an exact repository name to list a single repository; references remain repository-qualified, even when different repositories contain a chart with the same name:

```csharp
var repositories = helm.ListRepositories();
var charts = helm.ListCharts(repositoryName: "bitnami");
var nginxVersions = charts
    .Where(chart => chart.Reference == "bitnami/nginx")
    .Select(chart => chart.Version);
```

Chart catalogs are read only from the indexes already present in Helm's repository cache. `ListCharts` performs no network requests and does not update indexes; call `UpdateRepositories()` when you want to refresh them. With no configured repositories, an unfiltered listing is empty. An unknown repository name, or a configured repository with a missing or malformed cached index, throws `HelmActionException` identifying the repository. This API enumerates indexed chart repositories only; it does not enumerate OCI registries. Use `SearchCharts(searchTerm)` when substring search across configured repository indexes is desired.

### Select a chart version

`Template`, `Install`, and `Upgrade` accept an optional `version` using the same Helm chart-resolution options:

```csharp
var selected = helm.SearchCharts("nginx").First();
var preview = helm.Template("my-release", selected.Reference, version: selected.Version);
var installed = helm.Install("my-release", selected.Reference, createNamespace: true, version: selected.Version);
var upgraded = helm.Upgrade("my-release", selected.Reference, installIfMissing: true, version: selected.Version);
```

The version is also honored when `Upgrade` installs a missing release. Omitting it (or passing `null`) preserves Helm's default resolution, which may select the latest version. Invalid or unavailable repository/OCI versions throw `HelmActionException` without falling back to another version. Values files, inline values, namespace creation, wait, and timeout options remain independent of chart version selection.

Chart references follow Helm semantics, including repository references and OCI references such as `oci://registry.example.com/charts/nginx`. OCI credentials use `HelmClientOptions.RegistryConfig`. Helm does not use `version` to select or validate the version of an already resolved local chart directory/archive or a direct chart archive URL.

`HelmRelease.ChartVersion` reports chart metadata; `HelmRelease.Revision` is the separate release history revision. The original `Install` and `Upgrade` signatures remain as forwarding overloads for existing compiled callers; new calls can append the optional `version` argument.

### Stored release rendering limitation

`TemplateRelease` renders only chart contents available in the stored release; it does not download charts or dependencies. Helm's release JSON retains dependency metadata but omits dependency chart objects and their templates. Consequently, a stored chart that includes a helper defined by a subchart can fail with `no template ... associated with template "gotpl"` (as observed with Authentik's remote-cluster helper). Those dependency contents cannot be reconstructed from metadata alone.

Use `GetManifest` to retrieve the exact deployed manifest without re-rendering, or `Template` with the original chart reference, explicit chart version, and appropriate values to render a chart with its packaged dependencies. `TemplateRelease` surfaces rendering failures rather than silently substituting a saved manifest or fetching potentially different dependencies.

The Go shim source and pinned Helm dependency are in `native/helm-shim`. Native libraries are packaged under `runtimes/<rid>/native`.

### Test organization and scenario coverage

`tests/Helm.NET.Tests` contains managed unit tests for request serialization, response mapping, and interop error handling. Reusable end-to-end scenarios live in `tests/Helm.NET.Tests.E2E.Shared`; both the managed Kind host (`tests/Helm.NET.Tests.E2E`) and the xUnit v3 NativeAOT host (`tests/Helm.NET.Tests.E2E.AOT`) run those same public-API scenarios.

The shared chart-version fixture serves three real packaged chart versions with a library subchart; no external repository is needed. Scenarios cover repository search and cached chart listing, explicit/default/constrained template versions, archive and direct-URL semantics, values merging, invalid/unavailable versions, selected/default/constrained Install and Upgrade versions, upgrade-to-older-version, install-if-missing, compatibility overloads, returned and stored chart metadata versus release revisions, preview/deployment equality, flags, release operations, and failed upgrades leaving the installed revision unchanged. They also verify stored subchart helper behavior and historical manifests.

The managed E2E workflow and NativeAOT workflow run the shared cluster scenarios against Kind. NativeAOT also runs the no-cluster scenarios across both target frameworks and all supported runtime identifiers. Native-only tests retain Helm internals and the local OCI protocol fixture, which requires a custom plain-HTTP registry client unavailable through the managed API.
