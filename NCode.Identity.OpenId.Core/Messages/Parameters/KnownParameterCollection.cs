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
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Messages.Parameters;

internal class KnownParameterCollection : IKnownParameterCollection
{
    public static KnownParameterCollection Default { get; } = new();

    // TODO: allow for additional/custom known parameters to be added/registered
    private static readonly Dictionary<string, KnownParameter> StaticKnownParameters = new(StringComparer.OrdinalIgnoreCase)
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
        [KnownParameters.ResponseTypes.Name] = KnownParameters.ResponseTypes,
        [KnownParameters.Scopes.Name] = KnownParameters.Scopes,
        [KnownParameters.State.Name] = KnownParameters.State,
        [KnownParameters.UiLocales.Name] = KnownParameters.UiLocales,
    };

    /// <inheritdoc />
    public int Count =>
        StaticKnownParameters.Count;

    /// <inheritdoc />
    public bool TryGet(string parameterName, [MaybeNullWhen(false)] out KnownParameter knownParameter) =>
        StaticKnownParameters.TryGetValue(parameterName, out knownParameter);

    /// <inheritdoc />
    public IEnumerator<KnownParameter> GetEnumerator() =>
        StaticKnownParameters.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
