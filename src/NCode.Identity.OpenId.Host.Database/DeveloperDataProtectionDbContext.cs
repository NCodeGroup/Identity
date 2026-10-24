#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
// See ADR-0073.
[PublicAPI]
public sealed class DeveloperDataProtectionDbContext(
    DbContextOptions<DeveloperDataProtectionDbContext> options
) : DbContext(options), IDataProtectionKeyContext
{
    /// <inheritdoc />
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}
