# Helm.NET

[![Nuget](https://img.shields.io/nuget/vpre/Helm.NET.svg?style=flat-square)](https://www.nuget.org/packages/Helm.NET)
[![Nuget)](https://img.shields.io/nuget/dt/Helm.NET.svg?style=flat-square)](https://www.nuget.org/packages/Helm.NET)
[![codecov](https://codecov.io/gh/IvanJosipovic/Helm.NET/graph/badge.svg?token=nZGzjHvaDh)](https://codecov.io/gh/IvanJosipovic/Helm.NET)

## What is this?

Helm.NET provides a .NET 11 client for the Helm command-line interface. It uses the bundled `helm` executable for the current platform by default and can also use an executable on `PATH` or an explicitly configured path.

```csharp
using Helm.NET;

var helm = new HelmClient();
var version = await helm.GetVersionAsync();

```
