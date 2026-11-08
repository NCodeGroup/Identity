// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// Implemented by an <see cref="ISecretKeyTypeDescriptor"/> whose keys a key management service can hold. RSA and
/// ECC descriptors implement it, and a contributed key type opts in the same way.
/// </summary>
// See ADR-1471.
[PublicAPI]
public interface IRemoteSecretKeyTypeDescriptor
{
    /// <summary>
    /// Creates the <see cref="SecretKey"/> for a key that a key management service holds, from its stored public
    /// half. The key is an <see cref="IRemoteSecretKey"/>.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="subjectPublicKeyInfo">The DER-encoded <c>SubjectPublicKeyInfo</c> of the key's public half.</param>
    /// <param name="binding">The binding of the key to its provider, reference and mapping row.</param>
    /// <returns>The remote <see cref="SecretKey"/>.</returns>
    SecretKey CreateRemoteKey(
        KeyMetadata metadata,
        ReadOnlySpan<byte> subjectPublicKeyInfo,
        RemoteKeyBinding binding
    );
}
