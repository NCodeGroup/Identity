// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Verifiers;

/// <summary>
/// Provides lookup over the registered <see cref="ISecretVerifierScheme"/> instances, by name.
/// </summary>
// See ADR-1391.
[PublicAPI]
public interface ISecretVerifierSchemeCatalog
{
    /// <summary>
    /// Gets the registered verifier schemes.
    /// </summary>
    IReadOnlyCollection<ISecretVerifierScheme> Schemes { get; }

    /// <summary>
    /// Looks up the scheme with the specified name.
    /// </summary>
    /// <param name="name">The name of the scheme, compared ordinally. See <see cref="SecretVerifierSchemes"/>.</param>
    /// <param name="scheme">When this method returns <see langword="true"/>, the matching scheme.</param>
    /// <returns><see langword="true"/> when a scheme is registered with the name; otherwise,
    /// <see langword="false"/>.</returns>
    bool TryGet(string name, [NotNullWhen(true)] out ISecretVerifierScheme? scheme);
}
