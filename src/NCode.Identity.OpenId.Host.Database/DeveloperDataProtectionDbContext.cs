// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// Host-owned context that stores the ASP.NET Core Data Protection key ring in the same dev Postgres database as the
/// OpenID data. Persisting the ring in the database — rather than a file under <c>App_Data/dp-keys</c> —
/// means a single database purge resets the OpenID data, the seeded signing key, and the key ring
/// together, with nothing left on the filesystem. Kept out of the shared <c>OpenIdDbContext</c> so the published
/// persistence library never forces a <c>DataProtectionKeys</c> table on other hosts.
/// </summary>
// See ADR-1073.
[PublicAPI]
public sealed class DeveloperDataProtectionDbContext(
    DbContextOptions<DeveloperDataProtectionDbContext> options
) : DbContext(options), IDataProtectionKeyContext
{
    /// <inheritdoc />
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}
