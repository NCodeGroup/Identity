using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Endpoints.Authorization.Messages;

internal class RequestClaim : IRequestClaim
{
    /// <inheritdoc />
    [JsonPropertyName(OpenIdConstants.Parameters.Essential)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Essential { get; set; }

    /// <inheritdoc />
    [JsonPropertyName(OpenIdConstants.Parameters.Value)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }

    /// <inheritdoc />
    [JsonPropertyName(OpenIdConstants.Parameters.Values)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<string>? Values { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?> ExtensionData { get; set; } = new();
}
