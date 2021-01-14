using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using NCode.Identity.DataContracts;

namespace NCode.Identity.Flows.Authorization
{
    public class AuthorizationRequest
    {
        public IReadOnlyDictionary<string, StringValues> RawValues { get; set; }

        // case-sensitive
        public IReadOnlySet<string> Scopes { get; set; }

        public ResponseTypes ResponseType { get; set; }

        public GrantType GrantType { get; set; }

        public string ClientId { get; set; }

        public string RedirectUri { get; set; }

        public string State { get; set; }

        public ResponseMode ResponseMode { get; set; }

        public string Nonce { get; set; }

        public DisplayType Display { get; set; }

        public PromptTypes Prompt { get; set; }

        public TimeSpan? MaxAge { get; set; }

        public IReadOnlyList<string> UiLocales { get; set; }

        public string IdTokenHint { get; set; }

        public string LoginHint { get; set; }

        public IReadOnlyList<string> AcrValues { get; set; }

        public IReadOnlyList<string> ClaimsLocales { get; set; }

        public AuthorizationRequestClaims Claims { get; set; }

        public string CodeChallenge { get; set; }

        public CodeChallengeMethod CodeChallengeMethod { get; set; }
    }
}
