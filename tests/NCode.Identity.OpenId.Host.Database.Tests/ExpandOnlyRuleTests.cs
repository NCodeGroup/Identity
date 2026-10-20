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

using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace NCode.Identity.OpenId.Host.Database.Tests;

public sealed class ExpandOnlyRuleTests
{
    private const string MigrationsNamespace = "NCode.Identity.OpenId.Host.Database.Migrations.";
    private const string Table = "Tenants";
    private const string Column = "IssuerHost";

    private static AlterColumnOperation CreateAlter(
        Type clrType,
        bool isNullable,
        int? maxLength,
        Type oldClrType,
        bool oldIsNullable,
        int? oldMaxLength,
        object? defaultValue = null
    ) =>
        new()
        {
            Table = Table,
            Name = Column,
            ClrType = clrType,
            IsNullable = isNullable,
            MaxLength = maxLength,
            DefaultValue = defaultValue,
            OldColumn = new AddColumnOperation
            {
                Table = Table,
                Name = Column,
                ClrType = oldClrType,
                IsNullable = oldIsNullable,
                MaxLength = oldMaxLength,
            },
        };

    #region FindContractingOperations Tests

    [Fact]
    public void FindContractingOperations_WhenOperationsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExpandOnlyRule.FindContractingOperations(null!));
    }

    [Fact]
    public void FindContractingOperations_WhenOperationsOnlyExpand_ReturnsEmpty()
    {
        MigrationOperation[] operations =
        [
            new CreateTableOperation { Name = "Keys" },
            new AddColumnOperation
            {
                Table = Table,
                Name = Column,
                ClrType = typeof(string),
                IsNullable = true,
            },
            new AddColumnOperation
            {
                Table = Table,
                Name = "Revision",
                ClrType = typeof(int),
                IsNullable = false,
                DefaultValue = 0,
            },
            new CreateIndexOperation
            {
                Table = Table,
                Name = "IX_Tenants_IssuerHost",
                Columns = [Column],
            },
            new DropIndexOperation { Table = Table, Name = "IX_Tenants_DomainName" },
            new SqlOperation { Sql = "UPDATE \"Tenants\" SET \"IssuerHost\" = NULL;" },
        ];

        var findings = ExpandOnlyRule.FindContractingOperations(operations);

        Assert.Empty(findings);
    }

    [Fact]
    public void FindContractingOperations_WhenSchemaDropped_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new DropSchemaOperation { Name = "identity" },
        ]);

        Assert.Equal(["drops schema identity"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenTableDropped_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new DropTableOperation { Name = Table },
        ]);

        Assert.Equal(["drops table Tenants"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenTableRenamed_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new RenameTableOperation { Name = Table, NewName = "Realms" },
        ]);

        Assert.Equal(["renames table Tenants to Realms"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenColumnDropped_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new DropColumnOperation { Table = Table, Name = Column },
        ]);

        Assert.Equal(["drops column Tenants.IssuerHost"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenColumnRenamed_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new RenameColumnOperation
            {
                Table = Table,
                Name = Column,
                NewName = "Host",
            },
        ]);

        Assert.Equal(["renames column Tenants.IssuerHost to Host"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenSequenceDropped_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new DropSequenceOperation { Name = "TenantIds" },
        ]);

        Assert.Equal(["drops sequence TenantIds"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenRequiredColumnAddedWithoutDefault_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new AddColumnOperation
            {
                Table = Table,
                Name = Column,
                ClrType = typeof(string),
                IsNullable = false,
            },
        ]);

        Assert.Equal(["adds required column Tenants.IssuerHost without a default"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenColumnMadeRequiredWithoutDefault_ReportsIt()
    {
        var alter = CreateAlter(typeof(string), false, 100, typeof(string), true, 100);

        var findings = ExpandOnlyRule.FindContractingOperations([alter]);

        Assert.Equal(["makes column Tenants.IssuerHost required without a default"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenColumnMadeRequiredWithDefault_ReturnsEmpty()
    {
        var alter = CreateAlter(
            typeof(string),
            false,
            100,
            typeof(string),
            true,
            100,
            defaultValue: ""
        );

        var findings = ExpandOnlyRule.FindContractingOperations([alter]);

        Assert.Empty(findings);
    }

    [Fact]
    public void FindContractingOperations_WhenColumnTypeChanges_ReportsIt()
    {
        var alter = CreateAlter(typeof(long), true, null, typeof(string), true, null);

        var findings = ExpandOnlyRule.FindContractingOperations([alter]);

        Assert.Equal(
            ["changes the type of column Tenants.IssuerHost from String to Int64"],
            findings
        );
    }

    [Fact]
    public void FindContractingOperations_WhenColumnShortened_ReportsIt()
    {
        var alter = CreateAlter(typeof(string), true, 100, typeof(string), true, 1000);

        var findings = ExpandOnlyRule.FindContractingOperations([alter]);

        Assert.Equal(["shortens column Tenants.IssuerHost"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenUnboundedColumnGetsMaximumLength_ReportsIt()
    {
        var alter = CreateAlter(typeof(string), true, 100, typeof(string), true, null);

        var findings = ExpandOnlyRule.FindContractingOperations([alter]);

        Assert.Equal(["shortens column Tenants.IssuerHost"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenColumnWidenedOrRelaxed_ReturnsEmpty()
    {
        var alter = CreateAlter(typeof(string), true, 1000, typeof(string), false, 100);

        var findings = ExpandOnlyRule.FindContractingOperations([alter]);

        Assert.Empty(findings);
    }

    #endregion

    #region Host Migration Tests

    public static TheoryData<string> HostMigrations
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var type in GetHostMigrationTypes())
                data.Add(type.FullName![MigrationsNamespace.Length..]);
            return data;
        }
    }

    private static IEnumerable<Type> GetHostMigrationTypes() =>
        typeof(HostDatabase)
            .Assembly.GetTypes()
            .Where(type =>
                typeof(Migration).IsAssignableFrom(type)
                && type is { IsAbstract: false }
                && type.GetCustomAttribute<MigrationAttribute>() is not null
            )
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

    private static (IReadOnlyList<string> Findings, ContractMigrationAttribute? Marker) Inspect(
        string migrationName
    )
    {
        var type = typeof(HostDatabase).Assembly.GetType(
            MigrationsNamespace + migrationName,
            throwOnError: true
        )!;
        var migration = (Migration)Activator.CreateInstance(type)!;
        migration.ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
        var findings = ExpandOnlyRule.FindContractingOperations(migration.UpOperations);
        return (findings, type.GetCustomAttribute<ContractMigrationAttribute>());
    }

    [Fact]
    public void HostMigrations_WhenEnumerated_FindsBothContexts()
    {
        var namespaces = GetHostMigrationTypes().Select(type => type.Namespace).Distinct().ToList();

        Assert.Contains(MigrationsNamespace + "OpenId", namespaces);
        Assert.Contains(MigrationsNamespace + "DataProtection", namespaces);
    }

    [Theory]
    [MemberData(nameof(HostMigrations))]
    public void HostMigration_WhenNotMarkedAsContractStep_OnlyExpands(string migrationName)
    {
        var (findings, marker) = Inspect(migrationName);
        if (marker is not null)
            return;

        Assert.True(
            findings.Count == 0,
            $"{migrationName} {string.Join("; ", findings)}, which the previous release still reads. Expand in this "
                + "release and contract in a later one, or mark a deliberate contract step [ContractMigration] (ADR-0180)."
        );
    }

    [Theory]
    [MemberData(nameof(HostMigrations))]
    public void HostMigration_WhenMarkedAsContractStep_ContractsAndStatesWhy(string migrationName)
    {
        var (findings, marker) = Inspect(migrationName);
        if (marker is null)
            return;

        Assert.NotEmpty(findings);
        Assert.False(string.IsNullOrWhiteSpace(marker.Reason));
    }

    #endregion
}
