// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Contributes rows to the open mapping from JOSE signature algorithms to key management service operations. A
/// package registers a source, so anyone adds rows by registering one.
/// </summary>
// See ADR-1471.
[PublicAPI]
public interface IRemoteSigningAlgorithmSource
{
    /// <summary>
    /// Gets the rows that the source contributes.
    /// </summary>
    IEnumerable<RemoteSigningAlgorithm> Algorithms { get; }
}
