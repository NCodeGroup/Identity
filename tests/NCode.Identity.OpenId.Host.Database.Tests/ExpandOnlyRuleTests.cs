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
    private const string ExpandReason = "Backfills a column the previous release does not read.";
    private const string ContractReason = "No running release reads the dropped column.";

    private static AlterColumnOperation CreateAlter(
        Type clrType,
        bool isNullable,
        int? maxLength,
        Type oldClrType,
        bool oldIsNullable,
        int? oldMaxLength,
        object? defaultValue = null,
        object? oldDefaultValue = null
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
                DefaultValue = oldDefaultValue,
            },
        };

    private static CreateTableOperation CreateConsentsTable(ReferentialAction onDelete) =>
        new()
        {
            Name = "Consents",
            ForeignKeys =
            {
                new AddForeignKeyOperation
                {
                    Table = "Consents",
                    Name = "FK_Consents_Tenants_TenantId",
                    Columns = ["TenantId"],
                    PrincipalTable = Table,
                    PrincipalColumns = ["Id"],
                    OnDelete = onDelete,
                },
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
    public void FindContractingOperations_WhenColumnMadeRequired_ReportsIt()
    {
        var alter = CreateAlter(typeof(string), false, 100, typeof(string), true, 100);

        var findings = ExpandOnlyRule.FindContractingOperations([alter]);

        Assert.Equal(["makes column Tenants.IssuerHost required"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenColumnMadeRequiredWithDefault_ReportsIt()
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

        Assert.Equal(["makes column Tenants.IssuerHost required"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenRequiredColumnLosesItsDefault_ReportsIt()
    {
        var alter = CreateAlter(
            typeof(string),
            false,
            100,
            typeof(string),
            false,
            100,
            oldDefaultValue: ""
        );

        var findings = ExpandOnlyRule.FindContractingOperations([alter]);

        Assert.Equal(["removes the default of required column Tenants.IssuerHost"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenRequiredColumnKeepsItsDefault_ReturnsEmpty()
    {
        var alter = CreateAlter(
            typeof(string),
            false,
            100,
            typeof(string),
            false,
            100,
            defaultValue: "",
            oldDefaultValue: ""
        );

        var findings = ExpandOnlyRule.FindContractingOperations([alter]);

        Assert.Empty(findings);
    }

    [Fact]
    public void FindContractingOperations_WhenPrecisionNarrows_ReportsIt()
    {
        var alter = new AlterColumnOperation
        {
            Table = Table,
            Name = Column,
            ClrType = typeof(decimal),
            IsNullable = true,
            Precision = 10,
            OldColumn = new AddColumnOperation
            {
                Table = Table,
                Name = Column,
                ClrType = typeof(decimal),
                IsNullable = true,
                Precision = 18,
            },
        };

        var findings = ExpandOnlyRule.FindContractingOperations([alter]);

        Assert.Equal(["narrows the precision or scale of column Tenants.IssuerHost"], findings);
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

    [Fact]
    public void FindContractingOperations_WhenUniqueIndexAddedToExistingTable_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new CreateIndexOperation
            {
                Table = Table,
                Name = "IX_Tenants_IssuerHost",
                Columns = [Column],
                IsUnique = true,
            },
        ]);

        Assert.Equal(
            ["adds unique index IX_Tenants_IssuerHost to existing table Tenants"],
            findings
        );
    }

    [Fact]
    public void FindContractingOperations_WhenUniqueIndexAddedToTableItCreates_ReturnsEmpty()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new CreateTableOperation { Name = "Keys" },
            new CreateIndexOperation
            {
                Table = "Keys",
                Name = "IX_Keys_Name",
                Columns = ["Name"],
                IsUnique = true,
            },
        ]);

        Assert.Empty(findings);
    }

    [Fact]
    public void FindContractingOperations_WhenUniqueConstraintAddedToExistingTable_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new AddUniqueConstraintOperation
            {
                Table = Table,
                Name = "AK_Tenants_IssuerHost",
                Columns = [Column],
            },
        ]);

        Assert.Equal(
            ["adds unique constraint AK_Tenants_IssuerHost to existing table Tenants"],
            findings
        );
    }

    [Fact]
    public void FindContractingOperations_WhenPrimaryKeyAddedToExistingTable_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new AddPrimaryKeyOperation
            {
                Table = Table,
                Name = "PK_Tenants",
                Columns = ["Id"],
            },
        ]);

        Assert.Equal(["adds primary key PK_Tenants to existing table Tenants"], findings);
    }

    [Fact]
    public void FindContractingOperations_WhenCheckConstraintAddedToExistingTable_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new AddCheckConstraintOperation
            {
                Table = Table,
                Name = "CK_Tenants_IssuerHost",
                Sql = "\"IssuerHost\" <> ''",
            },
        ]);

        Assert.Equal(
            ["adds check constraint CK_Tenants_IssuerHost to existing table Tenants"],
            findings
        );
    }

    [Fact]
    public void FindContractingOperations_WhenForeignKeyAddedToExistingTable_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new AddForeignKeyOperation
            {
                Table = Table,
                Name = "FK_Tenants_Servers_ServerId",
                Columns = ["ServerId"],
                PrincipalTable = "Servers",
                PrincipalColumns = ["Id"],
            },
        ]);

        Assert.Equal(
            ["adds foreign key FK_Tenants_Servers_ServerId to existing table Tenants"],
            findings
        );
    }

    [Fact]
    public void FindContractingOperations_WhenTableItCreatesRestrictsDeletesFromExistingTable_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            CreateConsentsTable(ReferentialAction.Restrict),
        ]);

        Assert.Equal(
            [
                "adds foreign key FK_Consents_Tenants_TenantId, which restricts deleting rows of existing table "
                    + "Tenants",
            ],
            findings
        );
    }

    [Fact]
    public void FindContractingOperations_WhenForeignKeyAddedToTableItCreatesRestrictsDeletesFromExistingTable_ReportsIt()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new CreateTableOperation { Name = "Consents" },
            new AddForeignKeyOperation
            {
                Table = "Consents",
                Name = "FK_Consents_Tenants_TenantId",
                Columns = ["TenantId"],
                PrincipalTable = Table,
                PrincipalColumns = ["Id"],
                OnDelete = ReferentialAction.NoAction,
            },
        ]);

        Assert.Equal(
            [
                "adds foreign key FK_Consents_Tenants_TenantId, which restricts deleting rows of existing table "
                    + "Tenants",
            ],
            findings
        );
    }

    [Fact]
    public void FindContractingOperations_WhenTableItCreatesCascadesFromExistingTable_ReturnsEmpty()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            CreateConsentsTable(ReferentialAction.Cascade),
        ]);

        Assert.Empty(findings);
    }

    [Fact]
    public void FindContractingOperations_WhenTablesItCreatesReferenceEachOther_ReturnsEmpty()
    {
        var findings = ExpandOnlyRule.FindContractingOperations([
            new CreateTableOperation { Name = "Keys" },
            new CreateTableOperation
            {
                Name = "KeyVersions",
                ForeignKeys =
                {
                    new AddForeignKeyOperation
                    {
                        Table = "KeyVersions",
                        Name = "FK_KeyVersions_Keys_KeyId",
                        Columns = ["KeyId"],
                        PrincipalTable = "Keys",
                        PrincipalColumns = ["Id"],
                        OnDelete = ReferentialAction.Restrict,
                    },
                },
            },
        ]);

        Assert.Empty(findings);
    }

    #endregion

    #region FindDataChanges Tests

    [Fact]
    public void FindDataChanges_WhenOperationsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExpandOnlyRule.FindDataChanges(null!));
    }

    [Fact]
    public void FindDataChanges_WhenOnlySchemaChanges_ReturnsEmpty()
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
            new CreateIndexOperation
            {
                Table = Table,
                Name = "IX_Tenants_IssuerHost",
                Columns = [Column],
            },
        ];

        var dataChanges = ExpandOnlyRule.FindDataChanges(operations);

        Assert.Empty(dataChanges);
    }

    [Fact]
    public void FindDataChanges_WhenSqlRuns_ReportsIt()
    {
        var dataChanges = ExpandOnlyRule.FindDataChanges([
            new SqlOperation { Sql = "UPDATE \"Tenants\" SET \"IssuerHost\" = NULL;" },
        ]);

        Assert.Equal(["runs raw SQL"], dataChanges);
    }

    [Fact]
    public void FindDataChanges_WhenSeedDataChanges_ReportsEachTable()
    {
        var dataChanges = ExpandOnlyRule.FindDataChanges([
            new InsertDataOperation { Table = Table },
            new UpdateDataOperation { Table = Table },
            new DeleteDataOperation { Table = Table },
        ]);

        Assert.Equal(
            ["inserts data into Tenants", "updates data in Tenants", "deletes data from Tenants"],
            dataChanges
        );
    }

    #endregion

    #region Check Tests

    private sealed class UnmarkedExpansion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddColumn<string>(name: Column, table: Table, nullable: true);
    }

    private sealed class UnmarkedDrop : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropColumn(name: Column, table: Table);
    }

    private sealed class UnmarkedSql : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Tenants\" SET \"IssuerHost\" = NULL;");
            migrationBuilder.Sql("DELETE FROM \"Tenants\" WHERE \"IssuerHost\" IS NULL;");
        }
    }

    [ExpandMigration(ExpandReason)]
    private sealed class ExpandMarkedSql : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("UPDATE \"Tenants\" SET \"IssuerHost\" = NULL;");
    }

    [ExpandMigration(ExpandReason)]
    private sealed class ExpandMarkedDrop : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Tenants\" SET \"IssuerHost\" = NULL;");
            migrationBuilder.DropColumn(name: Column, table: Table);
        }
    }

    [ExpandMigration(ExpandReason)]
    private sealed class ExpandMarkedExpansion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddColumn<string>(name: Column, table: Table, nullable: true);
    }

    [ContractMigration(ContractReason)]
    private sealed class ContractMarkedSql : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("UPDATE \"Tenants\" SET \"IssuerHost\" = NULL;");
    }

    [ContractMigration(ContractReason)]
    private sealed class ContractMarkedExpansion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddColumn<string>(name: Column, table: Table, nullable: true);
    }

    [ContractMigration(" ")]
    private sealed class ContractMarkedWithoutReason : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropColumn(name: Column, table: Table);
    }

    [ExpandMigration(" ")]
    private sealed class ExpandMarkedWithoutReason : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("UPDATE \"Tenants\" SET \"IssuerHost\" = NULL;");
    }

    [ExpandMigration(ExpandReason)]
    [ContractMigration(ContractReason)]
    private sealed class DoublyMarkedSql : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("UPDATE \"Tenants\" SET \"IssuerHost\" = NULL;");
    }

    [Fact]
    public void Check_WhenMigrationIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExpandOnlyRule.Check(null!));
    }

    [Fact]
    public void Check_WhenUnmarkedMigrationOnlyExpands_ReturnsEmpty()
    {
        var violations = ExpandOnlyRule.Check(new UnmarkedExpansion());

        Assert.Empty(violations);
    }

    [Fact]
    public void Check_WhenUnmarkedMigrationContracts_ReportsIt()
    {
        var violations = ExpandOnlyRule.Check(new UnmarkedDrop());

        Assert.Equal(
            [
                "It drops column Tenants.IssuerHost, which the previous release still reads; expand in this release "
                    + "and contract in a later one, or mark a deliberate contract step [ContractMigration].",
            ],
            violations
        );
    }

    [Fact]
    public void Check_WhenUnmarkedMigrationChangesData_ReportsIt()
    {
        var violations = ExpandOnlyRule.Check(new UnmarkedSql());

        Assert.Equal(
            [
                "It runs raw SQL, which the rule cannot classify; mark it [ExpandMigration] when the previous release "
                    + "still reads every row it writes, or [ContractMigration] when it does not.",
            ],
            violations
        );
    }

    [Fact]
    public void Check_WhenExpandMarkedMigrationChangesData_ReturnsEmpty()
    {
        var violations = ExpandOnlyRule.Check(new ExpandMarkedSql());

        Assert.Empty(violations);
    }

    [Fact]
    public void Check_WhenExpandMarkedMigrationContracts_ReportsIt()
    {
        var violations = ExpandOnlyRule.Check(new ExpandMarkedDrop());

        Assert.Equal(
            [
                "It is marked [ExpandMigration] but drops column Tenants.IssuerHost; "
                    + "mark it [ContractMigration] instead.",
            ],
            violations
        );
    }

    [Fact]
    public void Check_WhenExpandMarkedMigrationChangesNoData_ReportsIt()
    {
        var violations = ExpandOnlyRule.Check(new ExpandMarkedExpansion());

        Assert.Equal(["It is marked [ExpandMigration] but changes no data."], violations);
    }

    [Fact]
    public void Check_WhenContractMarkedMigrationChangesData_ReturnsEmpty()
    {
        var violations = ExpandOnlyRule.Check(new ContractMarkedSql());

        Assert.Empty(violations);
    }

    [Fact]
    public void Check_WhenContractMarkedMigrationOnlyExpands_ReportsIt()
    {
        var violations = ExpandOnlyRule.Check(new ContractMarkedExpansion());

        Assert.Equal(
            ["It is marked [ContractMigration] but only expands the schema and changes no data."],
            violations
        );
    }

    [Fact]
    public void Check_WhenMarkerStatesNoReason_ReportsIt()
    {
        var violations = ExpandOnlyRule.Check(new ContractMarkedWithoutReason());

        Assert.Equal(["Its [ContractMigration] marker states no reason."], violations);
    }

    [Fact]
    public void Check_WhenExpandMarkerStatesNoReason_ReportsIt()
    {
        var violations = ExpandOnlyRule.Check(new ExpandMarkedWithoutReason());

        Assert.Equal(["Its [ExpandMigration] marker states no reason."], violations);
    }

    [Fact]
    public void Check_WhenBothMarkers_ReportsIt()
    {
        var violations = ExpandOnlyRule.Check(new DoublyMarkedSql());

        Assert.Equal(["It carries both [ExpandMigration] and [ContractMigration]."], violations);
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

    private static Migration CreateMigration(string migrationName)
    {
        var type = typeof(HostDatabase).Assembly.GetType(
            MigrationsNamespace + migrationName,
            throwOnError: true
        )!;
        var migration = (Migration)Activator.CreateInstance(type)!;
        migration.ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
        return migration;
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
    public void HostMigration_WhenChecked_KeepsThePreviousReleaseWorking(string migrationName)
    {
        var migration = CreateMigration(migrationName);

        var violations = ExpandOnlyRule.Check(migration);

        Assert.True(
            violations.Count == 0,
            $"{migrationName}: {string.Join(" ", violations)} See ADR-0184 and ADR-0250."
        );
    }

    #endregion
}
