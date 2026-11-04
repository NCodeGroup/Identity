// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Cose.Algorithms;

/// <summary>
/// Provides the COSE algorithms that are registered and available, each bound to its JOSE implementation. An algorithm
/// whose JOSE implementation is not registered, such as EdDSA without the EdDSA package, is not in the catalog.
/// </summary>
// See ADR-1409.
[PublicAPI]
public interface ICoseAlgorithmCatalog
{
    /// <summary>
    /// Gets the available algorithm bindings.
    /// </summary>
    IReadOnlyCollection<CoseAlgorithmBinding> Algorithms { get; }

    /// <summary>
    /// Finds an available algorithm by its IANA identifier.
    /// </summary>
    /// <param name="identifier">The IANA identifier.</param>
    /// <param name="binding">When this method returns <c>true</c>, the binding.</param>
    /// <returns><c>true</c> when the algorithm is registered and available; otherwise, <c>false</c>.</returns>
    bool TryGet(int identifier, [NotNullWhen(true)] out CoseAlgorithmBinding? binding);
}
