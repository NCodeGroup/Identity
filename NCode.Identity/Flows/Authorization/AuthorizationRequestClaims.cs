using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NCode.Identity.Flows.Authorization
{
    public class AuthorizationRequestClaims
    {
        [JsonPropertyName("userinfo")]
        public Dictionary<string, AuthorizationRequestClaim> UserInfo { get; set; }

        [JsonPropertyName("id_token")]
        public Dictionary<string, AuthorizationRequestClaim> IdToken { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; }
    }
}
