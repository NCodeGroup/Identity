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
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// Finds the schema operations that would break the release still serving while a migration runs. The previous release
/// keeps serving during the rollout and after a rollback, so a migration only expands the schema unless it carries
/// <see cref="ContractMigrationAttribute"/>.
/// </summary>
/// <remarks>
/// The rule reads schema operations only. Raw SQL is opaque to it, so a data change in a migration stays a review item.
/// </remarks>
// See ADR-0180 and ADR-0184.
[PublicAPI]
public static class ExpandOnlyRule
{
    /// <summary>
    /// Describes every operation in <paramref name="operations"/> that the previous release could not run against: a
    /// dropped or renamed schema, table, column, or sequence; a column made required without a default; a column whose
    /// type changes or whose maximum length shrinks; and a required column added to an existing table without a default.
    /// </summary>
    /// <param name="operations">The operations a migration's <c>Up</c> method builds.</param>
    /// <returns>One description per contracting operation, in order; empty when the operations only expand.</returns>
    public static IReadOnlyList<string> FindContractingOperations(
        IEnumerable<MigrationOperation> operations
    )
    {
        ArgumentNullException.ThrowIfNull(operations);

        var findings = new List<string>();
        foreach (var operation in operations)
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
            }
        }

        return findings;
    }

    private static void AddAlterFindings(AlterColumnOperation alter, List<string> findings)
    {
        var column = $"{alter.Table}.{alter.Name}";
        var old = alter.OldColumn;

        if (!alter.IsNullable && old.IsNullable && !HasDefault(alter))
            findings.Add($"makes column {column} required without a default");

        if (alter.ClrType != old.ClrType)
            findings.Add(
                $"changes the type of column {column} from {old.ClrType.Name} to {alter.ClrType.Name}"
            );

        if (
            alter.MaxLength is { } maxLength
            && (old.MaxLength is null || maxLength < old.MaxLength)
        )
            findings.Add($"shortens column {column}");
    }

    private static bool HasDefault(ColumnOperation column) =>
        column.DefaultValue is not null
        || column.DefaultValueSql is not null
        || column.ComputedColumnSql is not null;
}
