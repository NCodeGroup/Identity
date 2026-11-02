// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Collections;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Provides the credentials of a client from a named key ring.
/// </summary>
/// <remarks>
/// A key ring names a keyed <c>ISecretKeyCollectionProvider</c>, so the keys can rotate and can live in a KMS, and a
/// store-backed source can replace this seam.
/// </remarks>
[PublicAPI]
public interface IClientKeyRingProvider
{
    /// <summary>
    /// Gets the keys of the specified key ring.
    /// </summary>
    /// <param name="keyRing">The name of the key ring.</param>
    /// <exception cref="InvalidOperationException">The key ring is not registered.</exception>
    ISecretKeyCollection GetKeyRing(string keyRing);
}
