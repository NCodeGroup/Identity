using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Messages
{
    public class RequestClaims
    {
        [JsonPropertyName(OpenIdConstants.Parameters.ClaimsUserinfo)]
        public Dictionary<string, RequestClaim> UserInfo { get; set; }

        [JsonPropertyName(OpenIdConstants.Parameters.ClaimsIdToken)]
        public Dictionary<string, RequestClaim> IdToken { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; }
    }
}
