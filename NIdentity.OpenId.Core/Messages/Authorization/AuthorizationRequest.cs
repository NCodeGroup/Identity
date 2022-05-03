#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System;
using System.Collections.Generic;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Authorization;

internal class AuthorizationRequest : IAuthorizationRequest
{
    public AuthorizationRequest(IAuthorizationRequestMessage requestMessage, IAuthorizationRequestObject? requestObject, Client client)
    {
        OriginalRequestMessage = requestMessage;
        OriginalRequestObject = requestObject;
        Client = client;
    }

    private static GrantType DetermineGrantType(ResponseTypes responseType) => responseType switch
    {
        ResponseTypes.Unspecified => GrantType.Unspecified,
        ResponseTypes.Code => GrantType.AuthorizationCode,
        _ => responseType.HasFlag(ResponseTypes.Code) ? GrantType.Hybrid : GrantType.Implicit
    };

    private static ResponseMode DetermineDefaultResponseNode(GrantType grantType) =>
        grantType == GrantType.AuthorizationCode ?
            ResponseMode.Query :
            ResponseMode.Fragment;

    public IAuthorizationRequestMessage OriginalRequestMessage { get; }

    public IAuthorizationRequestObject? OriginalRequestObject { get; }

    /*
     * The Authorization Server MUST assemble the set of Authorization Request parameters to be used from the Request Object value and the
     * OAuth 2.0 Authorization Request parameters (minus the request or request_uri parameters). If the same parameter exists both in the
     * Request Object and the OAuth Authorization Request parameters, the parameter in the Request Object is used. Using the assembled set
     * of Authorization Request parameters, the Authorization Server then validates the request the normal manner for the flow being used,
     * as specified in Sections 3.1.2.2, 3.2.2.2, or 3.3.2.2.
     */

    public IReadOnlyCollection<string> AcrValues => OriginalRequestObject?.AcrValues ?? OriginalRequestMessage.AcrValues ?? Array.Empty<string>();

    public IRequestClaims? Claims => OriginalRequestObject?.Claims ?? OriginalRequestMessage.Claims;

    public IReadOnlyCollection<string> ClaimsLocales => OriginalRequestObject?.ClaimsLocales ?? OriginalRequestMessage.ClaimsLocales ?? Array.Empty<string>();

    public Client Client { get; }

    public string ClientId => OriginalRequestObject?.ClientId ?? OriginalRequestMessage.ClientId ?? throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ClientId);

    public string? CodeChallenge => OriginalRequestObject?.CodeChallenge ?? OriginalRequestMessage.CodeChallenge;

    public CodeChallengeMethod CodeChallengeMethod => OriginalRequestObject?.CodeChallengeMethod ?? OriginalRequestMessage.CodeChallengeMethod ?? CodeChallengeMethod.Plain;

    public string? CodeVerifier => OriginalRequestObject?.CodeVerifier ?? OriginalRequestMessage.CodeVerifier;

    public DisplayType DisplayType => OriginalRequestObject?.DisplayType ?? OriginalRequestMessage.DisplayType ?? DisplayType.Page;

    public GrantType GrantType => DetermineGrantType(ResponseType);

    public string? IdTokenHint => OriginalRequestObject?.IdTokenHint ?? OriginalRequestMessage.IdTokenHint;

    public string? LoginHint => OriginalRequestObject?.LoginHint ?? OriginalRequestMessage.LoginHint;

    public TimeSpan? MaxAge => OriginalRequestObject?.MaxAge ?? OriginalRequestMessage.MaxAge;

    public string? Nonce => OriginalRequestObject?.Nonce ?? OriginalRequestMessage.Nonce;

    public PromptTypes PromptType => OriginalRequestObject?.PromptType ?? OriginalRequestMessage.PromptType ?? PromptTypes.Unspecified;

    public Uri RedirectUri => OriginalRequestObject?.RedirectUri ?? OriginalRequestMessage.RedirectUri ?? throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.RedirectUri);

    public ResponseMode ResponseMode => OriginalRequestObject?.ResponseMode ?? OriginalRequestMessage.ResponseMode ?? DetermineDefaultResponseNode(GrantType);

    public ResponseTypes ResponseType => OriginalRequestObject?.ResponseType ?? OriginalRequestMessage.ResponseType ?? throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ResponseType);

    public IReadOnlyCollection<string> Scopes => OriginalRequestObject?.Scopes ?? OriginalRequestMessage.Scopes ?? throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.Scope);

    public string? State => OriginalRequestObject?.State ?? OriginalRequestMessage.State;

    public IReadOnlyCollection<string> UiLocales => OriginalRequestObject?.UiLocales ?? OriginalRequestMessage.UiLocales ?? Array.Empty<string>();
}