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

The action API supports `ListReleases`, `SearchCharts`, `Template`, `TemplateRelease`, `GetManifest`, `Install`, `Upgrade`, `Uninstall`, `Status`, `History`, `Rollback`, `GetValues`, and chart repository operations. `HelmRelease.ChartName` and `ChartVersion` expose the chart metadata separately from the display string in `Chart`. Configure kubeconfig, context, namespace, repository paths, registry config, and the release storage driver with `HelmClientOptions`.

The Go shim source and pinned Helm dependency are in `native/helm-shim`. Native libraries are packaged under `runtimes/<rid>/native`.
