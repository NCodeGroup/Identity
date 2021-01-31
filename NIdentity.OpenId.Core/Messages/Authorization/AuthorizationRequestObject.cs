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

namespace NIdentity.OpenId.Messages.Authorization
{
    internal class AuthorizationRequestObject : IAuthorizationRequestObject
    {
        public IEnumerable<string>? AcrValues { get; set; }

        public IRequestClaims? Claims { get; set; }

        public IEnumerable<string>? ClaimsLocales { get; set; }

        public string? ClientId { get; set; }

        public string? CodeChallenge { get; set; }

        public CodeChallengeMethod? CodeChallengeMethod { get; set; }

        public string? CodeVerifier { get; set; }

        public DisplayType? Display { get; set; }

        public string? IdTokenHint { get; set; }

        public string? LoginHint { get; set; }

        public TimeSpan? MaxAge { get; set; }

        public string? Nonce { get; set; }

        public PromptTypes? Prompt { get; set; }

        public string? RedirectUri { get; set; }

        public ResponseMode? ResponseMode { get; set; }

        public ResponseTypes? ResponseType { get; set; }

        public IEnumerable<string>? Scopes { get; set; }

        public string? State { get; set; }

        public IEnumerable<string>? UiLocales { get; set; }
    }
}
