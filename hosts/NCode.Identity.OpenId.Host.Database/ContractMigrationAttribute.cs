// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// Marks a migration as a deliberate contract step: it drops, renames, or tightens schema that an earlier release
/// reads, or it changes data so that release fails on or loses a value it reads, and it ships only once no running
/// release reads that shape any more. A migration with neither marker only expands the schema and changes no data,
/// which <see cref="ExpandOnlyRule.Check"/> checks for every migration.
/// </summary>
// See ADR-1180.
[PublicAPI]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ContractMigrationAttribute(string reason) : Attribute
{
    /// <summary>
    /// Gets why the contract step is safe: which release stopped reading the shape this migration removes.
    /// </summary>
    public string Reason { get; } = reason;
}
