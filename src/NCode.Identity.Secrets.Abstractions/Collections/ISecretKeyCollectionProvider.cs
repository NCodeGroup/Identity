// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Collections.Providers;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Collections;

/// <summary>
/// Provides the composition root (i.e. top-level collection) of <see cref="SecretKey"/> instances by
/// aggregating multiple data sources and providing change notifications.
/// </summary>
[PublicAPI]
public interface ISecretKeyCollectionProvider : ICollectionProvider<SecretKey, ISecretKeyCollection>
{
    // nothing
}
