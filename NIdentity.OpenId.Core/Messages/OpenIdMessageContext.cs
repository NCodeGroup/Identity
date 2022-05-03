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
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Messages.Parameters;

namespace NIdentity.OpenId.Messages;

internal class OpenIdMessageContext : IOpenIdMessageContext
{
    private static readonly IReadOnlyDictionary<string, KnownParameter> StaticKnownParameters = new Dictionary<string, KnownParameter>(StringComparer.Ordinal)
    {
        [KnownParameters.AcrValues.Name] = KnownParameters.AcrValues,
        [KnownParameters.Claims.Name] = KnownParameters.Claims,
        [KnownParameters.ClaimsLocales.Name] = KnownParameters.ClaimsLocales,
        [KnownParameters.ClientId.Name] = KnownParameters.ClientId,
        [KnownParameters.CodeChallenge.Name] = KnownParameters.CodeChallenge,
        [KnownParameters.CodeChallengeMethod.Name] = KnownParameters.CodeChallengeMethod,
        [KnownParameters.CodeVerifier.Name] = KnownParameters.CodeVerifier,
        [KnownParameters.DisplayType.Name] = KnownParameters.DisplayType,
        [KnownParameters.IdTokenHint.Name] = KnownParameters.IdTokenHint,
        [KnownParameters.LoginHint.Name] = KnownParameters.LoginHint,
        [KnownParameters.MaxAge.Name] = KnownParameters.MaxAge,
        [KnownParameters.Nonce.Name] = KnownParameters.Nonce,
        [KnownParameters.PromptType.Name] = KnownParameters.PromptType,
        [KnownParameters.RedirectUri.Name] = KnownParameters.RedirectUri,
        [KnownParameters.RequestJwt.Name] = KnownParameters.RequestJwt,
        [KnownParameters.RequestUri.Name] = KnownParameters.RequestUri,
        [KnownParameters.ResponseMode.Name] = KnownParameters.ResponseMode,
        [KnownParameters.ResponseType.Name] = KnownParameters.ResponseType,
        [KnownParameters.Scopes.Name] = KnownParameters.Scopes,
        [KnownParameters.State.Name] = KnownParameters.State,
        [KnownParameters.UiLocales.Name] = KnownParameters.UiLocales,
    };

    public ILogger Logger { get; }

    public JsonSerializerOptions JsonSerializerOptions { get; }

    public OpenIdMessageContext(ILogger logger)
    {
        Logger = logger;
        JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,

            Converters =
            {
                new RequestClaimJsonConverter(),
                new RequestClaimsJsonConverter(),
                new OpenIdMessageJsonConverterFactory(this)
            }
        };
    }

    public bool TryGetKnownParameter(string parameterName, [NotNullWhen(true)] out KnownParameter? knownParameter)
    {
        return StaticKnownParameters.TryGetValue(parameterName, out knownParameter);
    }
}