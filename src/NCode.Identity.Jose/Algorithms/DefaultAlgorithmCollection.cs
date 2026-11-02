// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides a default implementation for the <see cref="IAlgorithmCollection"/> interface.
/// </summary>
internal class DefaultAlgorithmCollection(IEnumerable<Algorithm> items) : IAlgorithmCollection
{
    private Dictionary<(AlgorithmType type, string code), Algorithm> AlgorithmLookup { get; } =
        items.ToDictionary(algorithm => (algorithm.Type, algorithm.Code));

    /// <inheritdoc />
    public int Count => AlgorithmLookup.Count;

    /// <inheritdoc />
    public IEnumerator<Algorithm> GetEnumerator() => AlgorithmLookup.Values.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public bool TryGetAlgorithm<T>(
        AlgorithmType algorithmType,
        string algorithmCode,
        [MaybeNullWhen(false)] out T algorithm
    )
        where T : Algorithm
    {
        if (
            AlgorithmLookup.TryGetValue((algorithmType, algorithmCode), out var baseAlgorithm)
            && baseAlgorithm is T typedAlgorithm
        )
        {
            algorithm = typedAlgorithm;
            return true;
        }

        algorithm = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetSignatureAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out SignatureAlgorithm algorithm
    ) => TryGetAlgorithm(AlgorithmType.DigitalSignature, algorithmCode, out algorithm);

    /// <inheritdoc />
    public bool TryGetKeyManagementAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out KeyManagementAlgorithm algorithm
    ) => TryGetAlgorithm(AlgorithmType.KeyManagement, algorithmCode, out algorithm);

    /// <inheritdoc />
    public bool TryGetAuthenticatedEncryptionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out AuthenticatedEncryptionAlgorithm algorithm
    ) => TryGetAlgorithm(AlgorithmType.AuthenticatedEncryption, algorithmCode, out algorithm);

    /// <inheritdoc />
    public bool TryGetCompressionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out CompressionAlgorithm algorithm
    ) => TryGetAlgorithm(AlgorithmType.Compression, algorithmCode, out algorithm);
}
