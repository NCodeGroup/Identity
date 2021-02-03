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
using System.Linq;

namespace NIdentity.OpenId.Messages.Authorization
{
    internal class AuthorizationRequest : IAuthorizationRequest
    {
        private readonly IAuthorizationRequestMessage _message;
        private readonly IAuthorizationRequestObject? _object;

        public AuthorizationRequest(IAuthorizationRequestMessage message, IAuthorizationRequestObject? @object)
        {
            _message = message;
            _object = @object;
        }

        private static GrantType DetermineGrantType(ResponseTypes? responseType) =>
            responseType switch
            {
                null => GrantType.Unknown,
                ResponseTypes.Unknown => GrantType.Unknown,
                ResponseTypes.Code => GrantType.AuthorizationCode,
                _ => responseType.Value.HasFlag(ResponseTypes.Code) ? GrantType.Hybrid : GrantType.Implicit
            };

        private static ResponseMode DetermineDefaultResponseNode(GrantType grantType) =>
            grantType == GrantType.AuthorizationCode ?
                ResponseMode.Query :
                ResponseMode.Fragment;

        public IAuthorizationRequestMessage Message => _message;

        public IAuthorizationRequestObject? Object => _object;

        public IEnumerable<string> AcrValues =>
            _object?.AcrValues ??
            _message.AcrValues ??
            Enumerable.Empty<string>();

        public IRequestClaims? Claims => _object?.Claims ?? _message.Claims;

        public IEnumerable<string> ClaimsLocales =>
            _object?.ClaimsLocales ??
            _message.ClaimsLocales ??
            Enumerable.Empty<string>();

        public string? ClientId => _object?.ClientId ?? _message.ClientId;

        public string? CodeChallenge => _object?.CodeChallenge ?? _message.CodeChallenge;

        public CodeChallengeMethod CodeChallengeMethod =>
            _object?.CodeChallengeMethod ??
            _message.CodeChallengeMethod ??
            CodeChallengeMethod.Plain;

        public string? CodeVerifier => _object?.CodeVerifier ?? _message.CodeVerifier;

        public DisplayType Display => _object?.Display ?? _message.Display ?? DisplayType.Unknown;

        public GrantType GrantType => DetermineGrantType(ResponseType);

        public string? IdTokenHint => _object?.IdTokenHint ?? _message.IdTokenHint;

        public string? LoginHint => _object?.LoginHint ?? _message.LoginHint;

        public TimeSpan? MaxAge => _object?.MaxAge ?? _message.MaxAge;

        public string? Nonce => _object?.Nonce ?? _message.Nonce;

        public PromptTypes Prompt => _object?.Prompt ?? _message.Prompt ?? PromptTypes.Unknown;

        public string? RedirectUri => _object?.RedirectUri ?? _message.RedirectUri;

        public ResponseMode ResponseMode =>
            _object?.ResponseMode ??
            _message.ResponseMode ??
            DetermineDefaultResponseNode(GrantType);

        public ResponseTypes ResponseType =>
            _object?.ResponseType ??
            _message.ResponseType ??
            ResponseTypes.Unknown;

        public IEnumerable<string> Scopes => _object?.Scopes ?? _message.Scopes ?? Enumerable.Empty<string>();

        public string? State => _object?.State ?? _message.State;

        public IEnumerable<string> UiLocales =>
            _object?.UiLocales ??
            _message.UiLocales ??
            Enumerable.Empty<string>();
    }
}
