using System.Text.Json;
using System.Text.Json.Serialization;

namespace Helm.NET;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NativeHelmRequest))]
[JsonSerializable(typeof(NativeHelmRelease))]
[JsonSerializable(typeof(NativeHelmAck))]
[JsonSerializable(typeof(NativeHelmRelease[]))]
[JsonSerializable(typeof(HelmRepository[]))]
[JsonSerializable(typeof(HelmChartSearchResult[]))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(string))]
internal partial class NativeJsonContext : JsonSerializerContext;
