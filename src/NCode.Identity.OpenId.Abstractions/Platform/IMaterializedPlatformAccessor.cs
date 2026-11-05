// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Provides access to the <see cref="OpenIdPlatform"/> that this replica materialized, for background work that runs
/// outside any request.
/// </summary>
[PublicAPI]
public interface IMaterializedPlatformAccessor
{
    /// <summary>
    /// Gets the <see cref="OpenIdPlatform"/> that this replica materialized, waiting until the replica materializes it:
    /// when the replica starts, or, when the store cannot be read then, at its first request.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to stop waiting.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the materialized <see cref="OpenIdPlatform"/>.</returns>
    ValueTask<OpenIdPlatform> GetPlatformAsync(CancellationToken cancellationToken);
}
