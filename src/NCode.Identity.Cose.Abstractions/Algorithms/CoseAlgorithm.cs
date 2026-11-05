// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Algorithms;

/// <summary>
/// Describes one COSE algorithm: its IANA identifier and name, its class and the JOSE algorithm that implements it.
/// Descriptors are registered data, so an application adds an algorithm by registering a descriptor and never by
/// editing the library.
/// </summary>
// See ADR-1409.
[PublicAPI]
public sealed class CoseAlgorithm
{
    /// <summary>
    /// Gets the value of the algorithm in the IANA COSE Algorithms registry.
    /// </summary>
    public required int Identifier { get; init; }

    /// <summary>
    /// Gets the name of the algorithm in the IANA COSE Algorithms registry.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the class of the algorithm.
    /// </summary>
    public required CoseAlgorithmKind Kind { get; init; }

    /// <summary>
    /// Gets the code of the JOSE algorithm that implements this algorithm, so that no cryptography is duplicated.
    /// </summary>
    public required string JoseAlgorithmCode { get; init; }

    /// <summary>
    /// Gets the size, in bytes, that a message authentication code is truncated to, or <c>null</c> when the tag is the
    /// full output. Only a <see cref="CoseAlgorithmKind.Mac"/> algorithm has a tag size.
    /// </summary>
    public int? TagSizeBytes { get; init; }
}
