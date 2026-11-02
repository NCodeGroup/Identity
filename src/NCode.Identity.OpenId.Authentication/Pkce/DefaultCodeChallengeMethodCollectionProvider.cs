// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
