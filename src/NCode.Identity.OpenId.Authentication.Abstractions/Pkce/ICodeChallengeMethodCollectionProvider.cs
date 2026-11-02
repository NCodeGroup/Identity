// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Pkce;

/// <summary>
/// Provides lookup over the registered <see cref="ICodeChallengeMethod"/> transformations, keyed by their
/// case-sensitive <c>code_challenge_method</c> value. Register a custom implementation to replace the default behavior.
/// </summary>
[PublicAPI]
public interface ICodeChallengeMethodCollectionProvider
{
    /// <summary>
    /// Gets the case-sensitive set of supported <c>code_challenge_method</c> values.
    /// </summary>
    IReadOnlyCollection<string> SupportedMethods { get; }

    /// <summary>
    /// Attempts to get the <see cref="ICodeChallengeMethod"/> transformation registered for the specified
    /// <c>code_challenge_method</c> value.
    /// </summary>
    /// <param name="method">The case-sensitive <c>code_challenge_method</c> value to look up.</param>
    /// <param name="codeChallengeMethod">When this method returns <see langword="true"/>, contains the matching
    /// <see cref="ICodeChallengeMethod"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a transformation is registered for the specified value; otherwise,
    /// <see langword="false"/>.</returns>
    bool TryGet(string method, [NotNullWhen(true)] out ICodeChallengeMethod? codeChallengeMethod);
}
