// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Collections.Providers;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Provides the composition root (i.e. top-level collection) of <see cref="KnownParameter"/> instances by
/// aggregating multiple data sources and providing change notifications.
/// </summary>
[PublicAPI]
public interface IKnownParameterCollectionProvider
    : ICollectionProvider<KnownParameter, IKnownParameterCollection>
{
    // nothing
}
