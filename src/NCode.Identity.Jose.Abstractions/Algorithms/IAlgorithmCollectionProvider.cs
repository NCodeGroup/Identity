// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Collections.Providers;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides the composition root (i.e. top-level collection) of <see cref="Algorithm"/> instances by
/// aggregating multiple data sources and providing change notifications.
/// </summary>
[PublicAPI]
public interface IAlgorithmCollectionProvider : ICollectionProvider<Algorithm, IAlgorithmCollection>
{
    // nothing
}
