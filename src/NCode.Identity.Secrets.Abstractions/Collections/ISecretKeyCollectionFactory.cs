// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Collections;

/// <summary>
/// Factory abstraction for creating new <see cref="ISecretKeyCollection"/> instances.
/// </summary>
[PublicAPI]
public interface ISecretKeyCollectionFactory
{
    /// <summary>
    /// Factory method that creates a new <see cref="ISecretKeyCollection"/> instance
    /// with the specified collection of <see cref="SecretKey"/> items.
    /// </summary>
    /// <param name="items">The collection of <see cref="SecretKey"/> items.</param>
    /// <returns>The newly created <see cref="ISecretKeyCollection"/> instance.</returns>
    ISecretKeyCollection Create(IEnumerable<SecretKey> items);
}
