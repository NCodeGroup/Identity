// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// Lets the EF Core tools construct <see cref="DeveloperDataProtectionDbContext"/> without booting the host. The
/// connection string is a design-time placeholder — migrations are generated from the model and never open a
/// connection.
/// </summary>
internal sealed class DeveloperDataProtectionDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<DeveloperDataProtectionDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=openiddb;Username=postgres;Password=postgres";

    public DeveloperDataProtectionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DeveloperDataProtectionDbContext>();
        optionsBuilder.UseDataProtectionNpgsql(DesignTimeConnectionString);

        return new DeveloperDataProtectionDbContext(optionsBuilder.Options);
    }
}
