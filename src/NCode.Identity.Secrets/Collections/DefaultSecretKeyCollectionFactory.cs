// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Collections;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretKeyCollectionFactory"/> abstraction.
/// </summary>
internal class DefaultSecretKeyCollectionFactory : ISecretKeyCollectionFactory
{
    /// <inheritdoc />
    public ISecretKeyCollection Create(IEnumerable<SecretKey> items) =>
        new DefaultSecretKeyCollection(items);
}
