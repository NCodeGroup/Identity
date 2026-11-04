// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose.Algorithms;

/// <summary>
/// Provides the default implementation of <see cref="ICoseAlgorithmCatalog"/>: the registered
/// <see cref="CoseAlgorithm"/> descriptors composed with the JOSE algorithm collection and validated as a whole when the
/// catalog is composed. A descriptor whose JOSE algorithm is not registered is left out; a descriptor that names a JOSE
/// algorithm of the wrong class, or that repeats an identifier, fails the composition rather than a later message.
/// </summary>
// See ADR-1409.
internal class DefaultCoseAlgorithmCatalog : ICoseAlgorithmCatalog
{
    private FrozenDictionary<int, CoseAlgorithmBinding> BindingsByIdentifier { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCoseAlgorithmCatalog"/> class.
    /// </summary>
    /// <param name="algorithms">The registered COSE algorithm descriptors.</param>
    /// <param name="algorithmCollectionProvider">The provider of the JOSE algorithms.</param>
    /// <exception cref="InvalidOperationException">A descriptor is <c>null</c>, has an empty name or JOSE algorithm
    /// code, repeats an identifier, has a tag size it may not have, or is bound to a JOSE algorithm of another class.</exception>
    public DefaultCoseAlgorithmCatalog(
        IEnumerable<CoseAlgorithm> algorithms,
        IAlgorithmCollectionProvider algorithmCollectionProvider
    )
    {
        ArgumentNullException.ThrowIfNull(algorithms);
        ArgumentNullException.ThrowIfNull(algorithmCollectionProvider);

        var joseAlgorithms = algorithmCollectionProvider.Collection;
        var seen = new HashSet<int>();
        var bindings = new Dictionary<int, CoseAlgorithmBinding>();
        foreach (var algorithm in algorithms)
        {
            if (algorithm is null)
                throw new InvalidOperationException("A registered COSE algorithm is null.");

            if (!seen.Add(algorithm.Identifier))
                throw new InvalidOperationException(
                    $"The COSE algorithm identifier {algorithm.Identifier} is registered more than once."
                );

            if (string.IsNullOrWhiteSpace(algorithm.Name))
                throw new InvalidOperationException(
                    $"The COSE algorithm {algorithm.Identifier} has an empty name."
                );

            if (string.IsNullOrWhiteSpace(algorithm.JoseAlgorithmCode))
                throw new InvalidOperationException(
                    $"The COSE algorithm {algorithm.Identifier} names no JOSE algorithm."
                );

            if (algorithm.TagSizeBytes is not null && algorithm.Kind != CoseAlgorithmKind.Mac)
                throw new InvalidOperationException(
                    $"The COSE algorithm {algorithm.Identifier} has a tag size but is not a message authentication code."
                );

            var algorithmType = algorithm.Kind switch
            {
                CoseAlgorithmKind.Signature or CoseAlgorithmKind.Mac =>
                    AlgorithmType.DigitalSignature,
                CoseAlgorithmKind.ContentEncryption => AlgorithmType.AuthenticatedEncryption,
                CoseAlgorithmKind.KeyDistribution => AlgorithmType.KeyManagement,
                _ => throw new InvalidOperationException(
                    $"The COSE algorithm {algorithm.Identifier} has an unknown kind."
                ),
            };

            if (
                !joseAlgorithms.TryGetAlgorithm<Algorithm>(
                    algorithmType,
                    algorithm.JoseAlgorithmCode,
                    out var joseAlgorithm
                )
            )
                continue;

            EnsureFits(algorithm, joseAlgorithm);
            bindings.Add(algorithm.Identifier, new CoseAlgorithmBinding(algorithm, joseAlgorithm));
        }

        BindingsByIdentifier = bindings.ToFrozenDictionary();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<CoseAlgorithmBinding> Algorithms => BindingsByIdentifier.Values;

    /// <inheritdoc />
    public bool TryGet(int identifier, [NotNullWhen(true)] out CoseAlgorithmBinding? binding) =>
        BindingsByIdentifier.TryGetValue(identifier, out binding);

    private static void EnsureFits(CoseAlgorithm algorithm, Algorithm joseAlgorithm)
    {
        var fits = algorithm.Kind switch
        {
            CoseAlgorithmKind.Signature => joseAlgorithm is SignatureAlgorithm signature
                && signature.AcceptsOnly<AsymmetricSecretKey>(),
            CoseAlgorithmKind.Mac => joseAlgorithm is SignatureAlgorithm mac
                && mac.AcceptsOnly<SymmetricSecretKey>(),
            CoseAlgorithmKind.ContentEncryption => joseAlgorithm
                is AuthenticatedEncryptionAlgorithm,
            _ => joseAlgorithm is KeyManagementAlgorithm,
        };

        if (!fits)
            throw new InvalidOperationException(
                $"The COSE algorithm {algorithm.Identifier} is bound to the JOSE algorithm '{joseAlgorithm.Code}', which is not a {algorithm.Kind} algorithm."
            );

        if (
            algorithm.TagSizeBytes is { } tagSize
            && joseAlgorithm is SignatureAlgorithm macAlgorithm
            && (tagSize < 8 || tagSize > macAlgorithm.GetSignatureSizeBytes(0))
        )
            throw new InvalidOperationException(
                $"The COSE algorithm {algorithm.Identifier} has a tag size of {tagSize} bytes, which is outside 8 to {macAlgorithm.GetSignatureSizeBytes(0)}."
            );
    }
}
