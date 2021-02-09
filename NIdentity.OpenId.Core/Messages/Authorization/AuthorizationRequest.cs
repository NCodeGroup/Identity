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
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Authorization
{
    internal class AuthorizationRequest : IAuthorizationRequest
    {
        private readonly IAuthorizationRequestMessage _requestMessage;
        private readonly IAuthorizationRequestObject? _requestObject;

        public AuthorizationRequest(IAuthorizationRequestMessage requestMessage, IAuthorizationRequestObject? requestObject)
        {
            _requestMessage = requestMessage;
            _requestObject = requestObject;
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

        public IAuthorizationRequestMessage OriginalRequestMessage => _requestMessage;

        public IAuthorizationRequestObject? OriginalRequestObject => _requestObject;

        public IReadOnlyCollection<string> AcrValues => _requestObject?.AcrValues ?? _requestMessage.AcrValues ?? Array.Empty<string>();

        public IRequestClaims? Claims => _requestObject?.Claims ?? _requestMessage.Claims;

        public IReadOnlyCollection<string> ClaimsLocales => _requestObject?.ClaimsLocales ?? _requestMessage.ClaimsLocales ?? Array.Empty<string>();

        public string ClientId => _requestObject?.ClientId ?? _requestMessage.ClientId ?? throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ClientId);

        public string? CodeChallenge => _requestObject?.CodeChallenge ?? _requestMessage.CodeChallenge;

        public CodeChallengeMethod CodeChallengeMethod => _requestObject?.CodeChallengeMethod ?? _requestMessage.CodeChallengeMethod ?? CodeChallengeMethod.Plain;

        public string? CodeVerifier => _requestObject?.CodeVerifier ?? _requestMessage.CodeVerifier;

        public DisplayType Display => _requestObject?.DisplayType ?? _requestMessage.DisplayType ?? DisplayType.Unspecified;

        public GrantType GrantType => DetermineGrantType(ResponseType);

        public string? IdTokenHint => _requestObject?.IdTokenHint ?? _requestMessage.IdTokenHint;

        public string? LoginHint => _requestObject?.LoginHint ?? _requestMessage.LoginHint;

        public TimeSpan? MaxAge => _requestObject?.MaxAge ?? _requestMessage.MaxAge;

        public string? Nonce => _requestObject?.Nonce ?? _requestMessage.Nonce;

        public PromptTypes Prompt => _requestObject?.PromptType ?? _requestMessage.PromptType ?? PromptTypes.Unspecified;

        public Uri RedirectUri => _requestObject?.RedirectUri ?? _requestMessage.RedirectUri ?? throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.RedirectUri);

        public ResponseMode ResponseMode => _requestObject?.ResponseMode ?? _requestMessage.ResponseMode ?? DetermineDefaultResponseNode(GrantType);

        public ResponseTypes ResponseType => _requestObject?.ResponseType ?? _requestMessage.ResponseType ?? throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.ResponseType);

        public IReadOnlyCollection<string> Scopes => _requestObject?.Scopes ?? _requestMessage.Scopes ?? throw OpenIdException.Factory.MissingParameter(OpenIdConstants.Parameters.Scope);

        public string? State => _requestObject?.State ?? _requestMessage.State;

        public IReadOnlyCollection<string> UiLocales => _requestObject?.UiLocales ?? _requestMessage.UiLocales ?? Array.Empty<string>();
    }
}
