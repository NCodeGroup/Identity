using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Messages
{
    public class RequestClaim
    {
        [JsonPropertyName(OpenIdConstants.Parameters.ClaimsEssential)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Essential { get; set; }

        [JsonPropertyName(OpenIdConstants.Parameters.ClaimsValue)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Value { get; set; }

        [JsonPropertyName(OpenIdConstants.Parameters.ClaimsValues)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[] Values { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; }
    }
}
