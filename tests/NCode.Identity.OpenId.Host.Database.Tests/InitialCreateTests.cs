// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace NCode.Identity.OpenId.Host.Database.Tests;

public sealed class InitialCreateTests
{
    private const string InitialCreate = "InitialCreate";

    public static TheoryData<string> HostMigrations => HostMigrationCatalog.GetNames();

    [Fact]
    public void HostMigrations_WhenEnumerated_BelongToTheTwoContexts()
    {
        var namespaces = HostMigrationCatalog
            .GetTypes()
            .Select(type => type.Namespace)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                $"{HostMigrationCatalog.MigrationsNamespace}.DataProtection",
                $"{HostMigrationCatalog.MigrationsNamespace}.OpenId",
            ],
            namespaces
        );
    }

    [Theory]
    [InlineData("OpenId")]
    [InlineData("DataProtection")]
    public void HostContext_WhenEnumerated_HoldsOnlyInitialCreate(string context)
    {
        var migrations = HostMigrationCatalog
            .GetTypes()
            .Where(type =>
                type.Namespace == $"{HostMigrationCatalog.MigrationsNamespace}.{context}"
            )
            .ToList();

        var migration = Assert.Single(migrations);
        Assert.Equal(InitialCreate, migration.Name);
        var id = migration.GetCustomAttribute<MigrationAttribute>()!.Id;
        Assert.EndsWith($"_{InitialCreate}", id, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(HostMigrations))]
    public void HostMigration_WhenRead_ChangesNoData(string migrationName)
    {
        var migration = HostMigrationCatalog.Create(migrationName);

        var dataChanges = ExpandOnlyRule.FindDataChanges([
            .. migration.UpOperations,
            .. migration.DownOperations,
        ]);

        Assert.True(dataChanges.Count == 0, $"{migrationName}: {string.Join(" ", dataChanges)}");
    }
}
