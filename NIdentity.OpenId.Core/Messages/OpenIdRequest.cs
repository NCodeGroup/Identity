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
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;

namespace NIdentity.OpenId.Messages
{
    internal class OpenIdRequest : OpenIdMessage, IOpenIdRequest
    {
        internal static GrantType DetermineGrantType(ResponseTypes responseType) =>
            responseType switch
            {
                ResponseTypes.Unknown => GrantType.Unknown,
                ResponseTypes.Code => GrantType.AuthorizationCode,
                _ => responseType.HasFlag(ResponseTypes.Code) ? GrantType.Hybrid : GrantType.Implicit
            };

        internal static ResponseMode DetermineDefaultResponseNode(GrantType grantType) =>
            grantType == GrantType.AuthorizationCode ?
                ResponseMode.Query :
                ResponseMode.Fragment;

        /// <inheritdoc />
        public GrantType GrantType => DetermineGrantType(ResponseType);

        /// <inheritdoc />
        public ResponseMode DefaultResponseMode => DetermineDefaultResponseNode(GrantType);

        /// <inheritdoc />
        public IEnumerable<StringSegment> Scope
        {
            get => GetKnownParameter(KnownParameters.Scope) ?? Enumerable.Empty<StringSegment>();
            set => SetKnownParameter(KnownParameters.Scope, value);
        }

        /// <inheritdoc />
        public ResponseTypes ResponseType
        {
            get => GetKnownParameter(KnownParameters.ResponseType);
            set => SetKnownParameter(KnownParameters.ResponseType, value);
        }

        /// <inheritdoc />
        public StringSegment ClientId
        {
            get => GetKnownParameter(KnownParameters.ClientId);
            set => SetKnownParameter(KnownParameters.ClientId, value);
        }

        /// <inheritdoc />
        public StringSegment RedirectUri
        {
            get => GetKnownParameter(KnownParameters.RedirectUri);
            set => SetKnownParameter(KnownParameters.RedirectUri, value);
        }

        /// <inheritdoc />
        public StringSegment State
        {
            get => GetKnownParameter(KnownParameters.State);
            set => SetKnownParameter(KnownParameters.State, value);
        }

        /// <inheritdoc />
        public ResponseMode ResponseMode
        {
            get => GetKnownParameter(KnownParameters.ResponseMode) ?? DefaultResponseMode;
            set => SetKnownParameter(KnownParameters.ResponseMode, value == DefaultResponseMode ? null : value);
        }

        /// <inheritdoc />
        public StringSegment Nonce
        {
            get => GetKnownParameter(KnownParameters.Nonce);
            set => SetKnownParameter(KnownParameters.Nonce, value);
        }

        /// <inheritdoc />
        public DisplayType? Display
        {
            get => GetKnownParameter(KnownParameters.Display);
            set => SetKnownParameter(KnownParameters.Display, value);
        }

        /// <inheritdoc />
        public PromptTypes? Prompt
        {
            get => GetKnownParameter(KnownParameters.Prompt);
            set => SetKnownParameter(KnownParameters.Prompt, value);
        }

        /// <inheritdoc />
        public TimeSpan? MaxAge
        {
            get => GetKnownParameter(KnownParameters.MaxAge);
            set => SetKnownParameter(KnownParameters.MaxAge, value);
        }

        /// <inheritdoc />
        public IEnumerable<StringSegment> UiLocales
        {
            get => GetKnownParameter(KnownParameters.UiLocales) ?? Enumerable.Empty<StringSegment>();
            set => SetKnownParameter(KnownParameters.UiLocales, value);
        }

        /// <inheritdoc />
        public StringSegment IdTokenHint
        {
            get => GetKnownParameter(KnownParameters.IdTokenHint);
            set => SetKnownParameter(KnownParameters.IdTokenHint, value);
        }

        /// <inheritdoc />
        public StringSegment LoginHint
        {
            get => GetKnownParameter(KnownParameters.LoginHint);
            set => SetKnownParameter(KnownParameters.LoginHint, value);
        }

        /// <inheritdoc />
        public IEnumerable<StringSegment> AcrValues
        {
            get => GetKnownParameter(KnownParameters.AcrValues) ?? Enumerable.Empty<StringSegment>();
            set => SetKnownParameter(KnownParameters.AcrValues, value);
        }

        /// <inheritdoc />
        public IEnumerable<StringSegment> ClaimsLocales
        {
            get => GetKnownParameter(KnownParameters.ClaimsLocales) ?? Enumerable.Empty<StringSegment>();
            set => SetKnownParameter(KnownParameters.ClaimsLocales, value);
        }

        // TODO: Claims

        /// <inheritdoc />
        public StringSegment CodeChallenge
        {
            get => GetKnownParameter(KnownParameters.CodeChallenge);
            set => SetKnownParameter(KnownParameters.CodeChallenge, value);
        }

        /// <inheritdoc />
        public CodeChallengeMethod CodeChallengeMethod
        {
            get => GetKnownParameter(KnownParameters.CodeChallengeMethod) ?? CodeChallengeMethod.Plain;
            set => SetKnownParameter(KnownParameters.CodeChallengeMethod, value);
        }

    }
}
