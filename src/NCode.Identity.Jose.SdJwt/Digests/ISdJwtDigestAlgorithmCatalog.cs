// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt.Digests;

/// <summary>
/// Provides lookup over the registered <see cref="SdJwtDigestAlgorithm"/> instances by the name an <c>_sd_alg</c>
/// claim carries.
/// </summary>
[PublicAPI]
public interface ISdJwtDigestAlgorithmCatalog
{
    /// <summary>
    /// Gets the registered hash algorithms.
    /// </summary>
    IReadOnlyCollection<SdJwtDigestAlgorithm> Algorithms { get; }

    /// <summary>
    /// Looks up the algorithm registered under the specified name.
    /// </summary>
    /// <param name="name">The algorithm name, compared ordinally.</param>
    /// <param name="algorithm">When this method returns <c>true</c>, the registered algorithm.</param>
    /// <returns><c>true</c> when an algorithm is registered under the name; otherwise <c>false</c>.</returns>
    bool TryGet(string name, [NotNullWhen(true)] out SdJwtDigestAlgorithm? algorithm);
}
