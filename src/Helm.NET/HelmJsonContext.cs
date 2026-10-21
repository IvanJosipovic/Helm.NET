using System.Text.Json;
using System.Text.Json.Serialization;

namespace Helm.NET;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NativeHelmRequest))]
[JsonSerializable(typeof(NativeHelmRelease))]
[JsonSerializable(typeof(NativeHelmAck))]
[JsonSerializable(typeof(HelmRelease[]))]
[JsonSerializable(typeof(HelmRepository[]))]
[JsonSerializable(typeof(HelmChartSearchResult[]))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>), TypeInfoPropertyName = "ValueDictionary")]
[JsonSerializable(typeof(string))]
internal partial class NativeJsonContext : JsonSerializerContext;
