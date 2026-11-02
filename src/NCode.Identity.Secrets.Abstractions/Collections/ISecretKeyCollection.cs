// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Collections;

/// <summary>
/// Provides a read-only collection of <see cref="SecretKey"/> instances that can be accessed by <c>Key ID (KID)</c>.
/// The collection is sorted descending by the <see cref="KeyMetadata.ExpiresWhen"/> property.
/// </summary>
[PublicAPI]
public interface ISecretKeyCollection : IReadOnlyCollection<SecretKey>
{
    /// <summary>
    /// Attempts to get a <see cref="SecretKey"/> with the specified <c>Key ID (KID)</c>.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> of the <see cref="SecretKey"/> to get.</param>
    /// <param name="secretKey">When this method returns, the <see cref="SecretKey"/> with the specified <c>Key ID (KID)</c>,
    /// if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a <see cref="SecretKey"/> was found with the specified <c>Key ID (KID)</c>; otherwise, <c>false</c>.</returns>
    bool TryGetByKeyId(string keyId, [MaybeNullWhen(false)] out SecretKey secretKey);
}
