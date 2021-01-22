using System;
using System.Collections.Generic;

namespace NIdentity.OpenId.Messages
{
    public interface IOpenIdAuthorizationRequest : IOpenIdMessage
    {
        GrantType GrantType { get; }

        ResponseMode DefaultResponseMode { get; }

        IEnumerable<string> Scope { get; set; }

        ResponseTypes ResponseType { get; set; }

        string? ClientId { get; set; }

        string? RedirectUri { get; set; }

        string? State { get; set; }

        ResponseMode ResponseMode { get; set; }

        string? Nonce { get; set; }

        DisplayType? Display { get; set; }

        PromptTypes? Prompt { get; set; }

        TimeSpan? MaxAge { get; set; }

        IEnumerable<string> UiLocales { get; set; }

        string? IdTokenHint { get; set; }

        string? LoginHint { get; set; }

        IEnumerable<string> AcrValues { get; set; }

        IEnumerable<string> ClaimsLocales { get; set; }

        string? Request { get; set; }

        string? RequestUri { get; set; }

        RequestClaims? Claims { get; set; }

        string? CodeVerifier { get; set; }

        string? CodeChallenge { get; set; }

        CodeChallengeMethod CodeChallengeMethod { get; set; }
    }
}
