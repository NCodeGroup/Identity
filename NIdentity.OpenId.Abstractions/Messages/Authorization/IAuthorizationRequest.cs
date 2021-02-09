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

namespace NIdentity.OpenId.Messages.Authorization
{
    public interface IAuthorizationRequest
    {
        IAuthorizationRequestMessage OriginalRequestMessage { get; }

        IAuthorizationRequestObject? OriginalRequestObject { get; }

        //

        IReadOnlyCollection<string> AcrValues { get; }

        IRequestClaims? Claims { get; }

        IReadOnlyCollection<string> ClaimsLocales { get; }

        Client Client { get; }

        string ClientId { get; }

        string? CodeChallenge { get; }

        CodeChallengeMethod CodeChallengeMethod { get; }

        string? CodeVerifier { get; }

        DisplayType Display { get; }

        GrantType GrantType { get; }

        string? IdTokenHint { get; }

        string? LoginHint { get; }

        TimeSpan? MaxAge { get; }

        string? Nonce { get; }

        PromptTypes PromptType { get; }

        Uri RedirectUri { get; }

        ResponseMode ResponseMode { get; }

        ResponseTypes ResponseType { get; }

        IReadOnlyCollection<string> Scopes { get; }

        string? State { get; }

        IReadOnlyCollection<string> UiLocales { get; }
    }
}
