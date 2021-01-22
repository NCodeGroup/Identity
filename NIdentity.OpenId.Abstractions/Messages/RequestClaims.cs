using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Messages
{
    public class RequestClaims
    {
        [JsonPropertyName(OpenIdConstants.Parameters.Userinfo)]
        public Dictionary<string, RequestClaim?>? Userinfo { get; set; }

        [JsonPropertyName(OpenIdConstants.Parameters.IdToken)]
        public Dictionary<string, RequestClaim?>? IdToken { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; } = new();
    }
}
