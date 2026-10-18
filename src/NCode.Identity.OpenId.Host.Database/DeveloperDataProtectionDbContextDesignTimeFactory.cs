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
