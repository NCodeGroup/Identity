using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NCode.Identity.Flows.Authorization
{
    public class JwtAuthorizationRequest
    {
        [JsonPropertyName(IdentityConstants.Parameters.Scope)]
        public string Scope { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.ResponseType)]
        public string ResponseType { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.ClientId)]
        public string ClientId { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.RedirectUri)]
        public string RedirectUri { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.State)]
        public string State { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.ResponseMode)]
        public string ResponseMode { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.Nonce)]
        public string Nonce { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.Display)]
        public string Display { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.Prompt)]
        public string Prompt { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.MaxAge)]
        public int? MaxAgeSeconds { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.UiLocales)]
        public string UiLocales { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.IdTokenHint)]
        public string IdTokenHint { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.LoginHint)]
        public string LoginHint { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.AcrValues)]
        public string AcrValues { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.ClaimsLocales)]
        public string ClaimsLocales { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.Claims)]
        public AuthorizationRequestClaims Claims { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.CodeChallenge)]
        public string CodeChallenge { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.CodeChallengeMethod)]
        public string CodeChallengeMethod { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.Request)]
        public JsonElement Request { get; set; }

        [JsonPropertyName(IdentityConstants.Parameters.RequestUri)]
        public JsonElement RequestUri { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; }
    }
}
