using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NCode.Identity.Flows.Authorization
{
    public class AuthorizationRequestClaim
    {
        [JsonPropertyName("essential")]
        public bool? Essential { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("values")]
        public string[] Values { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; }
    }
}
