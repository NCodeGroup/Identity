// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Jose.SdJwt.Digests;

/// <summary>
/// Provides the default implementation of <see cref="ISdJwtDigestAlgorithmCatalog"/> over the registered
/// <see cref="SdJwtDigestAlgorithm"/> instances, validated as a whole when the catalog is composed: an algorithm
/// without a name, two algorithms with one name, or an algorithm whose hash the platform cannot compute fails the
/// composition rather than a later presentation.
/// </summary>
internal class DefaultSdJwtDigestAlgorithmCatalog : ISdJwtDigestAlgorithmCatalog
{
    private FrozenDictionary<string, SdJwtDigestAlgorithm> AlgorithmsByName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSdJwtDigestAlgorithmCatalog"/> class.
    /// </summary>
    /// <param name="algorithms">The registered hash algorithms.</param>
    /// <exception cref="InvalidOperationException">An algorithm is <c>null</c>, has an empty or whitespace name, shares
    /// its name with another, or names a hash the platform cannot compute.</exception>
    public DefaultSdJwtDigestAlgorithmCatalog(IEnumerable<SdJwtDigestAlgorithm> algorithms)
    {
        ArgumentNullException.ThrowIfNull(algorithms);

        var algorithmsByName = new Dictionary<string, SdJwtDigestAlgorithm>(StringComparer.Ordinal);
        foreach (var algorithm in algorithms)
        {
            if (algorithm is null)
                throw new InvalidOperationException(
                    "A registered SD-JWT digest algorithm is null."
                );

            if (string.IsNullOrWhiteSpace(algorithm.Name))
                throw new InvalidOperationException(
                    "A registered SD-JWT digest algorithm has an empty name."
                );

            if (!algorithmsByName.TryAdd(algorithm.Name, algorithm))
                throw new InvalidOperationException(
                    $"The SD-JWT digest algorithm '{algorithm.Name}' is registered more than once."
                );

            try
            {
                _ = algorithm.DigestSizeBytes;
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"The SD-JWT digest algorithm '{algorithm.Name}' names a hash the platform cannot compute.",
                    exception
                );
            }
        }

        AlgorithmsByName = algorithmsByName.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<SdJwtDigestAlgorithm> Algorithms => AlgorithmsByName.Values;

    /// <inheritdoc />
    public bool TryGet(string name, [NotNullWhen(true)] out SdJwtDigestAlgorithm? algorithm) =>
        AlgorithmsByName.TryGetValue(name, out algorithm);
}
