# Helm.NET

[![Nuget](https://img.shields.io/nuget/vpre/Helm.NET.svg?style=flat-square)](https://www.nuget.org/packages/Helm.NET)
[![Nuget)](https://img.shields.io/nuget/dt/Helm.NET.svg?style=flat-square)](https://www.nuget.org/packages/Helm.NET)
[![codecov](https://codecov.io/gh/IvanJosipovic/Helm.NET/graph/badge.svg?token=nZGzjHvaDh)](https://codecov.io/gh/IvanJosipovic/Helm.NET)

## What is this?

Helm.NET provides a .NET 11 client for the Helm 4.3 command-line interface. It uses the bundled `helm` executable for the current platform by default and can also use an executable on `PATH` or an explicitly configured path. It is a CLI wrapper: Helm and Kubernetes configuration, credentials, plugins, and repository settings follow the normal Helm behavior.

```csharp
using Helm.NET;

var helm = new HelmClient();
var version = await helm.GetVersionAsync();
await helm.AddRepositoryAsync("bitnami", "https://charts.bitnami.com/bitnami");
await helm.UpdateRepositoriesAsync();
await helm.InstallAsync(new HelmInstallOptions(
    "my-release", "bitnami/nginx", Namespace: "apps", CreateNamespace: true, Wait: true));

```

List releases as records, or use `ListAsync` when you need Helm's raw output:

```csharp
IReadOnlyList<HelmRelease> releases = await helm.ListReleasesAsync(
    new HelmListOptions(Namespace: "apps"));

foreach (var release in releases)
    Console.WriteLine($"{release.Name}: {release.Status} ({release.Chart})");
```

Common operations also have convenience methods: `UpgradeAsync`, `UninstallAsync`, `StatusAsync`, `RollbackAsync`, `HistoryAsync`, `GetValuesAsync`, `LintAsync`, and `TemplateAsync`. Any Helm command or plugin can be called through `ExecuteAsync`.

Configure executable lookup and command execution with `HelmClientOptions`:

```csharp
var helm = new HelmClient(new HelmClientOptions
{
    ExecutablePath = "helm", // omit to use the bundled executable
    WorkingDirectory = Environment.CurrentDirectory,
    CommandTimeout = TimeSpan.FromMinutes(5)
});
```

Nonzero exit codes, launch failures, cancellation, and timeouts throw `HelmCommandException`. It includes captured standard output and error where available. `CancellationToken` can also be passed to each asynchronous operation.
