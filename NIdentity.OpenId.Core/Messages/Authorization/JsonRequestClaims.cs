using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Messages.Authorization
{
    internal class JsonRequestClaims : IRequestClaims
    {
        [JsonPropertyName(OpenIdConstants.Parameters.UserInfo)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonRequestClaimDictionary? UserInfo { get; set; }

        [JsonPropertyName(OpenIdConstants.Parameters.IdToken)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonRequestClaimDictionary? IdToken { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; } = new();

        IReadOnlyDictionary<string, IRequestClaim?>? IRequestClaims.UserInfo => UserInfo;

        IReadOnlyDictionary<string, IRequestClaim?>? IRequestClaims.IdToken => IdToken;
    }
}
