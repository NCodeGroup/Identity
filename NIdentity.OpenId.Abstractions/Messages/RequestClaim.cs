using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Messages
{
    public class RequestClaim
    {
        [JsonPropertyName(OpenIdConstants.Parameters.Essential)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Essential { get; set; }

        [JsonPropertyName(OpenIdConstants.Parameters.Value)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Value { get; set; }

        [JsonPropertyName(OpenIdConstants.Parameters.Values)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? Values { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; } = new();
    }
}
