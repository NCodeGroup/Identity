// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Provides the registered <see cref="RateLimitDescriptor"/> instances by the stage that judges them.
/// </summary>
// See ADR-1246.
internal interface IRateLimitDescriptorCatalog
{
    /// <summary>
    /// Gets the descriptors of a stage in the order they are judged: by <see cref="RateLimitDescriptor.Order"/>, then by
    /// <see cref="RateLimitDescriptor.Name"/>.
    /// </summary>
    /// <param name="stage">The name of the stage, such as one of the <see cref="RateLimitStages"/>.</param>
    /// <returns>The descriptors of the stage, which is empty when none registered for it.</returns>
    ImmutableArray<RateLimitDescriptor> GetStage(string stage);
}
