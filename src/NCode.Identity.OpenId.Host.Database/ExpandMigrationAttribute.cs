// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// Marks a migration whose data change keeps the previous release working: every row its raw SQL or seed-data change
/// writes is one the previous release does not read, or reads without failing, with the same meaning or a restriction
/// this release applies too.
/// </summary>
// See ADR-1184 and ADR-1250.
[PublicAPI]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ExpandMigrationAttribute(string reason) : Attribute
{
    /// <summary>
    /// Gets why the data change keeps the previous release working.
    /// </summary>
    public string Reason { get; } = reason;
}
