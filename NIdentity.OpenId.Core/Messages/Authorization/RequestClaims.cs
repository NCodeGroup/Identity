using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Messages.Authorization
{
    internal class RequestClaims : IRequestClaims
    {
        [JsonPropertyName(OpenIdConstants.Parameters.UserInfo)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RequestClaimDictionary? UserInfo { get; set; }

        [JsonPropertyName(OpenIdConstants.Parameters.IdToken)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RequestClaimDictionary? IdToken { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object?> ExtensionData { get; set; } = new();

        IReadOnlyDictionary<string, IRequestClaim?>? IRequestClaims.UserInfo => UserInfo;

        IReadOnlyDictionary<string, IRequestClaim?>? IRequestClaims.IdToken => IdToken;
    }
}
