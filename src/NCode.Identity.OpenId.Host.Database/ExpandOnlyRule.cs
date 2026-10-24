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
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// Finds the schema operations that would break the release still serving while a migration runs. The previous release
/// keeps serving during the rollout and after a rollback, so a migration only expands the schema unless it carries
/// <see cref="ContractMigrationAttribute"/>.
/// </summary>
/// <remarks>
/// The rule classifies schema operations itself. Raw SQL and seed-data changes are opaque to it, so a migration that
/// makes one states what it does to the previous release with a marker.
/// </remarks>
// See ADR-0180 and ADR-0184.
[PublicAPI]
public static class ExpandOnlyRule
{
    /// <summary>
    /// Describes every operation in <paramref name="operations"/> that the previous release could not run against: a
    /// dropped or renamed schema, table, column, or sequence; a column made required, with or without a default; a
    /// required column whose default is removed; a column whose type changes or whose maximum length, precision or
    /// scale shrinks; a required column added to an existing table without a default; a unique index, unique
    /// constraint, primary key, check constraint or foreign key added to an existing table; and a foreign key of a
    /// table the operations create that restricts deleting rows of an existing table. A table exists unless the same
    /// operations create it.
    /// </summary>
    /// <param name="operations">The operations a migration's <c>Up</c> method builds.</param>
    /// <returns>One description per contracting operation, in order; empty when the operations only expand.</returns>
    public static IReadOnlyList<string> FindContractingOperations(
        IEnumerable<MigrationOperation> operations
    )
    {
        ArgumentNullException.ThrowIfNull(operations);

        List<MigrationOperation> buffered = [.. operations];
        var createdTables = new HashSet<(string? Schema, string Name)>();
        foreach (var create in buffered.OfType<CreateTableOperation>())
            createdTables.Add((create.Schema, create.Name));

        bool IsExisting(string? schema, string table) => !createdTables.Contains((schema, table));

        var findings = new List<string>();
        foreach (var operation in buffered)
        {
            switch (operation)
            {
                case DropSchemaOperation drop:
                    findings.Add($"drops schema {drop.Name}");
                    break;

                case DropTableOperation drop:
                    findings.Add($"drops table {drop.Name}");
                    break;

                case RenameTableOperation rename:
                    findings.Add($"renames table {rename.Name} to {rename.NewName ?? rename.Name}");
                    break;

                case DropColumnOperation drop:
                    findings.Add($"drops column {drop.Table}.{drop.Name}");
                    break;

                case RenameColumnOperation rename:
                    findings.Add(
                        $"renames column {rename.Table}.{rename.Name} to {rename.NewName}"
                    );
                    break;

                case DropSequenceOperation drop:
                    findings.Add($"drops sequence {drop.Name}");
                    break;

                case AddColumnOperation add when !add.IsNullable && !HasDefault(add):
                    findings.Add($"adds required column {add.Table}.{add.Name} without a default");
                    break;

                case AlterColumnOperation alter:
                    AddAlterFindings(alter, findings);
                    break;

                case CreateIndexOperation { IsUnique: true } index
                    when IsExisting(index.Schema, index.Table):
                    findings.Add($"adds unique index {index.Name} to existing table {index.Table}");
                    break;

                case AddUniqueConstraintOperation unique
                    when IsExisting(unique.Schema, unique.Table):
                    findings.Add(
                        $"adds unique constraint {unique.Name} to existing table {unique.Table}"
                    );
                    break;

                case AddPrimaryKeyOperation key when IsExisting(key.Schema, key.Table):
                    findings.Add($"adds primary key {key.Name} to existing table {key.Table}");
                    break;

                case AddCheckConstraintOperation check when IsExisting(check.Schema, check.Table):
                    findings.Add(
                        $"adds check constraint {check.Name} to existing table {check.Table}"
                    );
                    break;

                case AddForeignKeyOperation foreignKey
                    when IsExisting(foreignKey.Schema, foreignKey.Table):
                    findings.Add(
                        $"adds foreign key {foreignKey.Name} to existing table {foreignKey.Table}"
                    );
                    break;

                case AddForeignKeyOperation foreignKey:
                    AddRestrictingForeignKeyFinding(foreignKey, createdTables, findings);
                    break;

                case CreateTableOperation create:
                    foreach (var foreignKey in create.ForeignKeys)
                        AddRestrictingForeignKeyFinding(foreignKey, createdTables, findings);
                    break;
            }
        }

        return findings;
    }

    /// <summary>
    /// Describes every operation in <paramref name="operations"/> that changes data, which the rule cannot classify, so
    /// the migration states its classification with a marker.
    /// </summary>
    /// <param name="operations">The operations a migration's <c>Up</c> method builds.</param>
    /// <returns>One description per data change, in order; empty when the operations change no data.</returns>
    public static IReadOnlyList<string> FindDataChanges(IEnumerable<MigrationOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var dataChanges = new List<string>();
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case SqlOperation:
                    dataChanges.Add("runs raw SQL");
                    break;

                case InsertDataOperation insert:
                    dataChanges.Add($"inserts data into {insert.Table}");
                    break;

                case UpdateDataOperation update:
                    dataChanges.Add($"updates data in {update.Table}");
                    break;

                case DeleteDataOperation delete:
                    dataChanges.Add($"deletes data from {delete.Table}");
                    break;
            }
        }

        return dataChanges;
    }

    /// <summary>
    /// Judges <paramref name="migration"/> against the rule. A migration without a marker only expands the schema and
    /// changes no data. A migration marked <see cref="ExpandMigrationAttribute"/> changes data and contracts nothing. A
    /// migration marked <see cref="ContractMigrationAttribute"/> contracts the schema or changes data. A marker states
    /// a reason, and a migration carries at most one marker.
    /// </summary>
    /// <param name="migration">
    /// The migration to judge. Its <c>ActiveProvider</c> must be set when its <c>Up</c> method reads it.
    /// </param>
    /// <returns>
    /// One sentence per violation, in order; empty when the migration keeps the previous release working.
    /// </returns>
    public static IReadOnlyList<string> Check(Migration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);

        var type = migration.GetType();
        var expand = type.GetCustomAttribute<ExpandMigrationAttribute>();
        var contract = type.GetCustomAttribute<ContractMigrationAttribute>();
        var contracting = FindContractingOperations(migration.UpOperations);
        var dataChanges = FindDataChanges(migration.UpOperations)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var violations = new List<string>();
        if (expand is not null && contract is not null)
        {
            violations.Add("It carries both [ExpandMigration] and [ContractMigration].");
        }
        else if (contract is not null)
        {
            if (string.IsNullOrWhiteSpace(contract.Reason))
                violations.Add("Its [ContractMigration] marker states no reason.");

            if (contracting.Count == 0 && dataChanges.Count == 0)
                violations.Add(
                    "It is marked [ContractMigration] but only expands the schema and changes no data."
                );
        }
        else if (expand is not null)
        {
            if (string.IsNullOrWhiteSpace(expand.Reason))
                violations.Add("Its [ExpandMigration] marker states no reason.");

            if (contracting.Count != 0)
                violations.Add(
                    $"It is marked [ExpandMigration] but {string.Join("; ", contracting)}; "
                        + "mark it [ContractMigration] instead."
                );

            if (dataChanges.Count == 0)
                violations.Add("It is marked [ExpandMigration] but changes no data.");
        }
        else
        {
            if (contracting.Count != 0)
                violations.Add(
                    $"It {string.Join("; ", contracting)}, which the previous release still reads; expand in this "
                        + "release and contract in a later one, or mark a deliberate contract step [ContractMigration]."
                );

            if (dataChanges.Count != 0)
                violations.Add(
                    $"It {string.Join("; ", dataChanges)}, which the rule cannot classify; mark it [ExpandMigration] "
                        + "when the previous release still reads every row it writes, "
                        + "or [ContractMigration] when it does not."
                );
        }

        return violations;
    }

    private static void AddRestrictingForeignKeyFinding(
        AddForeignKeyOperation foreignKey,
        HashSet<(string? Schema, string Name)> createdTables,
        List<string> findings
    )
    {
        if (
            foreignKey.OnDelete is ReferentialAction.Restrict or ReferentialAction.NoAction
            && !createdTables.Contains((foreignKey.PrincipalSchema, foreignKey.PrincipalTable))
        )
            findings.Add(
                $"adds foreign key {foreignKey.Name}, which restricts deleting rows of existing table "
                    + foreignKey.PrincipalTable
            );
    }

    private static void AddAlterFindings(AlterColumnOperation alter, List<string> findings)
    {
        var column = $"{alter.Table}.{alter.Name}";
        var old = alter.OldColumn;

        if (!alter.IsNullable && old.IsNullable)
            findings.Add($"makes column {column} required");
        else if (!alter.IsNullable && HasDefault(old) && !HasDefault(alter))
            findings.Add($"removes the default of required column {column}");

        if (alter.ClrType != old.ClrType)
            findings.Add(
                $"changes the type of column {column} from {old.ClrType.Name} to {alter.ClrType.Name}"
            );

        if (Narrows(alter.MaxLength, old.MaxLength))
            findings.Add($"shortens column {column}");

        if (Narrows(alter.Precision, old.Precision) || Narrows(alter.Scale, old.Scale))
            findings.Add($"narrows the precision or scale of column {column}");
    }

    private static bool Narrows(int? bound, int? oldBound) =>
        bound is { } value && (oldBound is null || value < oldBound);

    private static bool HasDefault(ColumnOperation column) =>
        column.DefaultValue is not null
        || column.DefaultValueSql is not null
        || column.ComputedColumnSql is not null;
}
