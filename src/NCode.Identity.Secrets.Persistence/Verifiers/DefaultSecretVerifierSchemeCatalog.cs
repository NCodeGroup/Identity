// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides the default implementation of <see cref="ISecretVerifierSchemeCatalog"/> over the registered
/// <see cref="ISecretVerifierScheme"/> instances. A scheme registered later under a name replaces the one before it.
/// </summary>
// See ADR-1391.
internal sealed class DefaultSecretVerifierSchemeCatalog : ISecretVerifierSchemeCatalog
{
    private FrozenDictionary<string, ISecretVerifierScheme> SchemesByName { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<ISecretVerifierScheme> Schemes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSecretVerifierSchemeCatalog"/> class.
    /// </summary>
    /// <param name="schemes">The registered secret verifier schemes.</param>
    /// <exception cref="InvalidOperationException">A scheme's name is not a run of printable ASCII characters other
    /// than <c>$</c>.</exception>
    public DefaultSecretVerifierSchemeCatalog(IEnumerable<ISecretVerifierScheme> schemes)
    {
        var map = new Dictionary<string, ISecretVerifierScheme>(StringComparer.Ordinal);
        foreach (var scheme in schemes)
        {
            if (!SecretVerifierFormat.IsValidSchemeName(scheme.Name))
            {
                throw new InvalidOperationException(
                    $"The secret verifier scheme name '{scheme.Name}' is not a run of printable ASCII characters other than '$'."
                );
            }

            map[scheme.Name] = scheme;
        }

        SchemesByName = map.ToFrozenDictionary(StringComparer.Ordinal);
        Schemes = SchemesByName.Values;
    }

    /// <inheritdoc />
    public bool TryGet(string name, [NotNullWhen(true)] out ISecretVerifierScheme? scheme) =>
        SchemesByName.TryGetValue(name, out scheme);
}
