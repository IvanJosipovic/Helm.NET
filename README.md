# Helm.NET

[![NuGet](https://img.shields.io/nuget/vpre/Helm.NET.svg?style=flat-square)](https://www.nuget.org/packages/Helm.NET)
[![Downloads](https://img.shields.io/nuget/dt/Helm.NET.svg?style=flat-square)](https://www.nuget.org/packages/Helm.NET)
[![codecov](https://codecov.io/gh/IvanJosipovic/Helm.NET/graph/badge.svg?token=nZGzjHvaDh)](https://codecov.io/gh/IvanJosipovic/Helm.NET)

Helm.NET is a typed .NET client for Helm. It calls Helm's Go action library through a native shim; it does not start the Helm CLI executable.

## Install

```sh
dotnet add package Helm.NET
```

Native libraries are included for Windows, Linux, and macOS on x64 and ARM64.

## Quick start

```csharp
using Helm.NET;
using System.Text.Json;

var helm = new HelmClient(new HelmClientOptions
{
    DefaultNamespace = "apps"
});

helm.RepoAdd("bitnami", "https://charts.bitnami.com/bitnami");
helm.RepoUpdate();

var charts = helm.SearchRepo("nginx", versions: true);
var preview = helm.Template("preview", "bitnami/nginx");
using var valuesJson = JsonDocument.Parse("""{"replicaCount":2}""");
var release = helm.Install("my-release", "bitnami/nginx",
    setValues: valuesJson.RootElement,
    wait: true);

var status = helm.Status("my-release");
var manifest = helm.GetManifest("my-release");
var values = helm.GetValues("my-release");
var replicas = values.GetProperty("replicaCount").GetInt32();
```

## API

| Area | Methods |
| --- | --- |
| Releases | `List`, `Status`, `History`, `GetValues`, `GetManifest` |
| Chart actions | `Template`, `Install`, `Upgrade`, `Rollback`, `Uninstall` |
| Repositories | `RepoAdd`, `RepoList`, `RepoRemove`, `RepoUpdate`, `SearchRepo` |

Methods return typed .NET values. `HelmRelease` includes chart name/version, app version, revision, description, notes, resources, and hooks when Helm provides them.

`setValues` accepts a `JsonElement` containing a JSON object. `GetValues` returns that object as a `JsonElement`; pass `revision` to read an older release revision or `all: true` to include chart defaults.

`namespaceName` can be supplied per call. If omitted, `HelmClientOptions.DefaultNamespace` applies. `List(allNamespaces: true)` lists releases across namespaces.

Chart actions accept values files and inline values, chart version selection, wait and timeout controls, dry-run, hook and post-renderer controls, and chart repository download settings. `SearchRepo` searches configured repository indexes; run `RepoUpdate` to refresh them. Repository and OCI chart references follow Helm resolution rules. OCI credentials use `RegistryConfig`.

## Client options

`HelmClientOptions` configures client-wide defaults:

| Option | Purpose |
| --- | --- |
| `Kubeconfig`, `KubeContext` | Kubernetes connection |
| `DefaultNamespace` | Namespace used when a call omits `namespaceName` |
| `RepositoryConfig`, `RepositoryCache` | Helm chart repository files |
| `RegistryConfig` | OCI registry credentials |
| `HelmDriver` | Helm release storage driver |

## Development

The Go shim and pinned Helm dependency are in `native/helm-shim`; runtime libraries are under `src/Helm.NET/runtimes`. Managed tests are in `tests/Helm.NET.Tests`. Shared Kind scenarios run through both the managed and NativeAOT hosts.
