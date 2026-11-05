// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Algorithms;

/// <summary>
/// Binds a registered <see cref="CoseAlgorithm"/> to the JOSE algorithm that implements it.
/// </summary>
/// <param name="algorithm">The COSE algorithm descriptor.</param>
/// <param name="joseAlgorithm">The JOSE algorithm that implements it.</param>
[PublicAPI]
public sealed class CoseAlgorithmBinding(
    CoseAlgorithm algorithm,
    NCode.Identity.Jose.Algorithms.Algorithm joseAlgorithm
)
{
    /// <summary>
    /// Gets the COSE algorithm descriptor.
    /// </summary>
    public CoseAlgorithm Algorithm { get; } =
        algorithm ?? throw new ArgumentNullException(nameof(algorithm));

    /// <summary>
    /// Gets the JOSE algorithm that implements the COSE algorithm.
    /// </summary>
    public NCode.Identity.Jose.Algorithms.Algorithm JoseAlgorithm { get; } =
        joseAlgorithm ?? throw new ArgumentNullException(nameof(joseAlgorithm));
}
