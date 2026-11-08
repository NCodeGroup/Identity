// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Contributes descriptors of remote key providers and rows to the open mapping from JOSE signature algorithms to
/// key management service operations. A package registers a source, so anyone adds rows by registering one.
/// </summary>
// See ADR-1474.
[PublicAPI]
public interface IRemoteKeyDescriptorSource
{
    /// <summary>
    /// Gets the remote key providers that the source describes. A source that only adds rows describes none.
    /// </summary>
    IEnumerable<RemoteKeyProviderDescriptor> Providers { get; }

    /// <summary>
    /// Gets the rows that the source contributes.
    /// </summary>
    IEnumerable<RemoteSigningAlgorithm> Algorithms { get; }
}
