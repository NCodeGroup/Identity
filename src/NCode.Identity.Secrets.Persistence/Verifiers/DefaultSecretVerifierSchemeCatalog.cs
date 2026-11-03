// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using NCode.Collections.Providers;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides the default implementation of <see cref="ISecretVerifierSchemeCatalog"/> over the registered
/// <see cref="ISecretVerifierScheme"/> instances. A scheme registered later under a name replaces the one before it.
/// Every registered scheme has a registered <see cref="SecretVerifierSchemeDescriptor"/> of its name, and every
/// descriptor names a registered scheme, so the names the options are validated against as the host starts are the
/// names the catalog serves.
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
    /// <param name="descriptorDataSources">The data sources that contribute the descriptors of the registered
    /// schemes.</param>
    /// <exception cref="InvalidOperationException">A scheme's name is not a run of printable ASCII characters other
    /// than <c>$</c>, a scheme has no descriptor, or a descriptor names no registered scheme.</exception>
    public DefaultSecretVerifierSchemeCatalog(
        IEnumerable<ISecretVerifierScheme> schemes,
        IEnumerable<ICollectionDataSource<SecretVerifierSchemeDescriptor>> descriptorDataSources
    )
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

        var describedNames = descriptorDataSources
            .SelectMany(dataSource => dataSource.Collection)
            .Select(descriptor => descriptor.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in map.Keys)
        {
            if (!describedNames.Contains(name))
            {
                throw new InvalidOperationException(
                    $"The secret verifier scheme '{name}' has no registered {nameof(SecretVerifierSchemeDescriptor)}."
                );
            }
        }

        foreach (var name in describedNames)
        {
            if (!map.ContainsKey(name))
            {
                throw new InvalidOperationException(
                    $"The {nameof(SecretVerifierSchemeDescriptor)} '{name}' names no registered secret verifier scheme."
                );
            }
        }

        SchemesByName = map.ToFrozenDictionary(StringComparer.Ordinal);
        Schemes = SchemesByName.Values;
    }

    /// <inheritdoc />
    public bool TryGet(string name, [NotNullWhen(true)] out ISecretVerifierScheme? scheme) =>
        SchemesByName.TryGetValue(name, out scheme);
}
