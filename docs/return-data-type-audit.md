# Helm.NET return data type audit

This report records the pre-fix API review of public `IHelmClient` return values traced through the Go shim to Helm v4.3.1. The identified fixes have since been applied and committed.

## Findings and remediation

| Finding | Source type and wire representation | Remediation |
|---|---|---|
| `HelmRelease.Updated` was `string` | `release.Info.LastDeployed` is Go `time.Time`; JSON encodes it as RFC 3339, including year 1 for zero time | Public and native DTO types are `DateTimeOffset`. |
| Hook `StartedAt` and `CompletedAt` were `string` | Helm hook execution fields are Go `time.Time`; zero values are omitted from JSON | Public and native DTO types are nullable `DateTimeOffset?`. |
| Release `Revision` was `string` | `release.Release.Version` is Go `int`; the shim formerly stringified it | Public/native DTO types are `int`; shim emits a JSON number. The DTO accepts legacy numeric strings from older packaged binaries. |
| Search `Description` and `AppVersion` were nullable | The shim emits Go strings; absent metadata is an empty string, not JSON null | Public properties are non-null `string` values defaulting to empty. |

## Other return data reviewed

- Release `Name`, `Namespace`, `Status`, `Chart`, `AppVersion`, `ChartName`, `ChartVersion`, `Description`, and `Notes` are strings appropriate to their source values. `Status` remains a string to tolerate Helm status additions.
- Release `Resources` is `JsonElement?`, preserving arbitrary Kubernetes objects and custom-resource fields.
- Release `Hooks` and hook `Events`/`DeletePolicies` use read-only collection interfaces with string-backed wire values. `Weight` is `int`.
- Search `Reference`, `ChartName`, and `Version` are strings; chart versions remain text to avoid constraining package version syntax in the public API.
- Repository `Name` and `Url` are strings.
- `Template` and `GetManifest` return text; `GetValues` returns open-ended JSON; mutation operations return `void` and propagate failures as `HelmActionException`.

## Source references

- Go response projections: `native/helm-shim/main.go` (`releaseView`, `chartSearch`, `repoList`).
- Helm v4.3.1 definitions: `pkg/release/v1/release.go`, `info.go`, and `hook.go` in the Go module cache.
- C# response and public models: `src/Helm.NET/HelmNativeModels.cs`, `HelmRelease.cs`, `HelmChartSearchResult.cs`, and `HelmRepository.cs`.
