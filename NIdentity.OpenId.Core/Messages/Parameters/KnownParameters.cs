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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Messages.Parsers;

namespace NIdentity.OpenId.Messages.Parameters
{
    internal static class KnownParameters
    {
        private static readonly Lazy<ConcurrentDictionary<string, KnownParameter>> LazyRegistry = new(
            LoadRegistry,
            LazyThreadSafetyMode.PublicationOnly);

        private static ConcurrentDictionary<string, KnownParameter> LoadRegistry() =>
            new(StringComparer.Ordinal)
            {
                [Scopes.Name] = Scopes,
                [ResponseType.Name] = ResponseType,
                [ResponseMode.Name] = ResponseMode,
                [ClientId.Name] = ClientId,
                [RedirectUri.Name] = RedirectUri,
                [State.Name] = State,
                [Nonce.Name] = Nonce,
                [DisplayType.Name] = DisplayType,
                [PromptType.Name] = PromptType,
                [MaxAge.Name] = MaxAge,
                [UiLocales.Name] = UiLocales,
                [IdTokenHint.Name] = IdTokenHint,
                [LoginHint.Name] = LoginHint,
                [AcrValues.Name] = AcrValues,
                [CodeChallenge.Name] = CodeChallenge,
                [CodeChallengeMethod.Name] = CodeChallengeMethod,
            };

        public static readonly KnownParameter<IReadOnlyCollection<string>?> Scopes = new(
            OpenIdConstants.Parameters.Scope,
            optional: false,
            allowMultipleValues: true,
            ParameterParsers.StringSet);

        public static readonly KnownParameter<ResponseTypes?> ResponseType = new(
            OpenIdConstants.Parameters.ResponseType,
            optional: false,
            allowMultipleValues: false,
            ParameterParsers.ResponseType);

        public static readonly KnownParameter<ResponseMode?> ResponseMode = new(
            OpenIdConstants.Parameters.ResponseMode,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.ResponseMode);

        public static readonly KnownParameter<string?> ClientId = new(
            OpenIdConstants.Parameters.ClientId,
            optional: false,
            allowMultipleValues: false,
            ParameterParsers.String);

        public static readonly KnownParameter<string?> RedirectUri = new(
            OpenIdConstants.Parameters.RedirectUri,
            optional: false,
            allowMultipleValues: false,
            ParameterParsers.String);

        public static readonly KnownParameter<string?> State = new(
            OpenIdConstants.Parameters.State,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.String);

        public static readonly KnownParameter<string?> Nonce = new(
            OpenIdConstants.Parameters.Nonce,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.String);

        public static readonly KnownParameter<DisplayType?> DisplayType = new(
            OpenIdConstants.Parameters.Display,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.DisplayType);

        public static readonly KnownParameter<PromptTypes?> PromptType = new(
            OpenIdConstants.Parameters.Prompt,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.PromptType);

        public static readonly KnownParameter<TimeSpan?> MaxAge = new(
            OpenIdConstants.Parameters.MaxAge,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.TimeSpan);

        public static readonly KnownParameter<IReadOnlyCollection<string>?> UiLocales = new(
            OpenIdConstants.Parameters.UiLocales,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.StringSet);

        public static readonly KnownParameter<string?> IdTokenHint = new(
            OpenIdConstants.Parameters.IdTokenHint,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.String);

        public static readonly KnownParameter<string?> LoginHint = new(
            OpenIdConstants.Parameters.LoginHint,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.String);

        public static readonly KnownParameter<IReadOnlyCollection<string>?> AcrValues = new(
            OpenIdConstants.Parameters.AcrValues,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.StringSet);

        public static readonly KnownParameter<IReadOnlyCollection<string>?> ClaimsLocales = new(
            OpenIdConstants.Parameters.ClaimsLocales,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.StringSet);

        public static readonly KnownParameter<string?> Request = new(
            OpenIdConstants.Parameters.Request,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.String);

        public static readonly KnownParameter<string?> RequestUri = new(
            OpenIdConstants.Parameters.RequestUri,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.String);

        public static readonly KnownParameter<string?> CodeVerifier = new(
            OpenIdConstants.Parameters.CodeVerifier,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.String);

        public static readonly KnownParameter<string?> CodeChallenge = new(
            OpenIdConstants.Parameters.CodeChallenge,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.String);

        public static readonly KnownParameter<CodeChallengeMethod?> CodeChallengeMethod = new(
            OpenIdConstants.Parameters.CodeChallengeMethod,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.CodeChallengeMethod);

        public static readonly KnownParameter<IRequestClaims?> Claims = new(
            OpenIdConstants.Parameters.Claims,
            optional: true,
            allowMultipleValues: false,
            ParameterParsers.RequestClaims);

        public static KnownParameter Register(KnownParameter knownParameter) =>
            LazyRegistry.Value.GetOrAdd(knownParameter.Name, knownParameter);

        public static bool TryGet(string parameterName, [NotNullWhen(true)] out KnownParameter? knownParameter) =>
            LazyRegistry.Value.TryGetValue(parameterName, out knownParameter);
    }
}
