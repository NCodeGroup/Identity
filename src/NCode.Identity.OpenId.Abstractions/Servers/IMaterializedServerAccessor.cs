// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides access to the <see cref="OpenIdServer"/> that this replica materialized, for background work that runs
/// outside any request.
/// </summary>
[PublicAPI]
public interface IMaterializedServerAccessor
{
    /// <summary>
    /// Gets the <see cref="OpenIdServer"/> that this replica materialized, waiting until the replica materializes it:
    /// when the replica starts, or, when the store cannot be read then, at its first request.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to stop waiting.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the materialized <see cref="OpenIdServer"/>.</returns>
    ValueTask<OpenIdServer> GetServerAsync(CancellationToken cancellationToken);
}
