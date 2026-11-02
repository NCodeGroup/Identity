// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Caching;

/// <summary>
/// The timing rules of a <see cref="RefreshingCache{TValue}"/> lookup.
/// </summary>
/// <param name="RefreshInterval">How long a fetched value is fresh.</param>
/// <param name="MinimumRefreshInterval">The minimum time between two fetch attempts for the same key.</param>
/// <param name="StaleLifetime">How long a value may still be served when a refresh fails.</param>
internal readonly record struct RefreshingCachePolicy(
    TimeSpan RefreshInterval,
    TimeSpan MinimumRefreshInterval,
    TimeSpan StaleLifetime
);
