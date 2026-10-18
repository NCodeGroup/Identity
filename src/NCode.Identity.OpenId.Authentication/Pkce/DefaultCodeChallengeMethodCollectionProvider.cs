#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Authentication.Pkce;

/// <summary>
/// Provides the default implementation of <see cref="ICodeChallengeMethodCollectionProvider"/> backed by the
/// <see cref="ICodeChallengeMethod"/> implementations registered in dependency injection.
/// </summary>
internal sealed class DefaultCodeChallengeMethodCollectionProvider
    : ICodeChallengeMethodCollectionProvider
{
    private FrozenDictionary<string, ICodeChallengeMethod> MethodsByName { get; }

    public DefaultCodeChallengeMethodCollectionProvider(
        IEnumerable<ICodeChallengeMethod> codeChallengeMethods
    )
    {
        MethodsByName = codeChallengeMethods.ToFrozenDictionary(
            method => method.Method,
            StringComparer.Ordinal
        );
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedMethods => MethodsByName.Keys;

    /// <inheritdoc />
    public bool TryGet(
        string method,
        [NotNullWhen(true)] out ICodeChallengeMethod? codeChallengeMethod
    ) => MethodsByName.TryGetValue(method, out codeChallengeMethod);
}
