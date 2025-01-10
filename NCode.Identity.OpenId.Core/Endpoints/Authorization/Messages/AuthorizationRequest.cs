#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Collections;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

internal class AuthorizationRequest(
    bool isContinuation,
    IAuthorizationRequestMessage requestMessage,
    IAuthorizationRequestObject? requestObject
) : IAuthorizationRequest
{
    private static string DetermineGrantType(IReadOnlyCollection<string> responseTypes) =>
        responseTypes.Contains(OpenIdConstants.ResponseTypes.Code) ?
            responseTypes.Count == 1 ?
                OpenIdConstants.GrantTypes.AuthorizationCode :
                OpenIdConstants.GrantTypes.Hybrid :
            OpenIdConstants.GrantTypes.Implicit;

    private static string DetermineDefaultResponseMode(string grantType) =>
        grantType == OpenIdConstants.GrantTypes.AuthorizationCode ?
            OpenIdConstants.ResponseModes.Query :
            OpenIdConstants.ResponseModes.Fragment;

    public AuthorizationSourceType AuthorizationSourceType => AuthorizationSourceType.Union;

    public OpenIdEnvironment OpenIdEnvironment => OriginalRequestMessage.OpenIdEnvironment;

    public bool IsContinuation { get; set; } = isContinuation;

    public IAuthorizationRequestMessage OriginalRequestMessage { get; } = requestMessage;

    public IAuthorizationRequestObject? OriginalRequestObject { get; } = requestObject;

    /*
     * The Authorization Server MUST assemble the set of Authorization Request parameters to be used from the Request Object value and the
     * OAuth 2.0 Authorization Request parameters (minus the request or request_uri parameters). If the same parameter exists both in the
     * Request Object and the OAuth Authorization Request parameters, the parameter in the Request Object is used. Using the assembled set
     * of Authorization Request parameters, the Authorization Server then validates the request the normal manner for the flow being used,
     * as specified in Sections 3.1.2.2, 3.2.2.2, or 3.3.2.2.
     */

    public IReadOnlyCollection<string> AcrValues =>
        OriginalRequestObject?.AcrValues ??
        OriginalRequestMessage.AcrValues ??
        Array.Empty<string>();

    public IRequestClaims? Claims =>
        OriginalRequestObject?.Claims ??
        OriginalRequestMessage.Claims;

    public IReadOnlyCollection<string> ClaimsLocales =>
        OriginalRequestObject?.ClaimsLocales ??
        OriginalRequestMessage.ClaimsLocales ??
        Array.Empty<string>();

    public string ClientId =>
        OriginalRequestObject?.ClientId ??
        OriginalRequestMessage.ClientId ??
        throw OpenIdEnvironment
            .ErrorFactory
            .MissingParameter(OpenIdConstants.Parameters.ClientId)
            .AsException();

    public string? CodeChallenge =>
        OriginalRequestObject?.CodeChallenge ??
        OriginalRequestMessage.CodeChallenge;

    public string CodeChallengeMethod =>
        OriginalRequestObject?.CodeChallengeMethod ??
        OriginalRequestMessage.CodeChallengeMethod ??
        OpenIdConstants.CodeChallengeMethods.Plain;

    public string? CodeVerifier =>
        OriginalRequestObject?.CodeVerifier ??
        OriginalRequestMessage.CodeVerifier;

    public string DisplayType =>
        OriginalRequestObject?.DisplayType ??
        OriginalRequestMessage.DisplayType ??
        OpenIdConstants.DisplayTypes.Page;

    public string GrantType =>
        DetermineGrantType(ResponseTypes);

    public string? IdTokenHint =>
        OriginalRequestObject?.IdTokenHint ??
        OriginalRequestMessage.IdTokenHint;

    public string? LoginHint =>
        OriginalRequestObject?.LoginHint ??
        OriginalRequestMessage.LoginHint;

    public TimeSpan? MaxAge =>
        OriginalRequestObject?.MaxAge ??
        OriginalRequestMessage.MaxAge;

    public string? Nonce =>
        OriginalRequestObject?.Nonce ??
        OriginalRequestMessage.Nonce;

    public IReadOnlyCollection<string> PromptTypes =>
        OriginalRequestObject?.PromptTypes ??
        OriginalRequestMessage.PromptTypes ??
        Array.Empty<string>();

    public Uri RedirectUri =>
        OriginalRequestObject?.RedirectUri ??
        OriginalRequestMessage.RedirectUri ??
        throw OpenIdEnvironment
            .ErrorFactory
            .MissingParameter(OpenIdConstants.Parameters.RedirectUri)
            .AsException();

    public string ResponseMode =>
        OriginalRequestObject?.ResponseMode ??
        OriginalRequestMessage.ResponseMode ??
        DetermineDefaultResponseMode(GrantType);

    public IReadOnlyCollection<string> ResponseTypes =>
        OriginalRequestObject?.ResponseTypes ??
        OriginalRequestMessage.ResponseTypes ??
        Array.Empty<string>();

    public IReadOnlyCollection<string> Scopes =>
        OriginalRequestObject?.Scopes ??
        OriginalRequestMessage.Scopes ??
        Array.Empty<string>();

    public string? State =>
        OriginalRequestObject?.State ??
        OriginalRequestMessage.State;

    public IReadOnlyCollection<string> UiLocales =>
        OriginalRequestObject?.UiLocales ??
        OriginalRequestMessage.UiLocales ??
        Array.Empty<string>();

    private IEnumerable<KeyValuePair<string, StringValues>> GetUnion()
    {
        if (OriginalRequestObject == null || OriginalRequestObject.Count == 0)
            return OriginalRequestMessage;

        return OriginalRequestMessage
            .ExceptBy(OriginalRequestObject.Keys, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Concat(OriginalRequestObject);
    }

    public int Count =>
        GetUnion().Count();

    public IEnumerable<string> Keys =>
        GetUnion().Select(kvp => kvp.Key);

    public IEnumerable<StringValues> Values =>
        GetUnion().Select(kvp => kvp.Value);

    public StringValues this[string key] =>
        OriginalRequestObject?.TryGetValue(key, out var value) ?? false ?
            value :
            OriginalRequestMessage[key];

    public bool ContainsKey(string key) =>
        OriginalRequestObject?.ContainsKey(key) ??
        OriginalRequestMessage.ContainsKey(key);

    public bool TryGetValue(string key, out StringValues value) =>
        OriginalRequestObject?.TryGetValue(key, out value) ??
        OriginalRequestMessage.TryGetValue(key, out value);

    [MustDisposeResource]
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() =>
        GetUnion().GetEnumerator();

    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
