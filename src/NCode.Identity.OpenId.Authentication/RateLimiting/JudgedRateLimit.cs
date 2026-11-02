// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// A <see cref="RateLimitDescriptor"/> that applies to a request, with the partition value the request counts in and
/// the limit and overage that the request's settings give it.
/// </summary>
/// <param name="Descriptor">The descriptor that applies to the request.</param>
/// <param name="PartitionValue">The partition of the limit that the request counts in.</param>
/// <param name="Limit">The most requests the limit admits in one window.</param>
/// <param name="Overage">The most requests admitted past <paramref name="Limit"/> as overage; zero when there is none.</param>
// See ADR-1246.
internal readonly record struct JudgedRateLimit(
    RateLimitDescriptor Descriptor,
    string PartitionValue,
    int Limit,
    int Overage
);
