// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Contains the names of the audit events that record the changes the server makes to a tenant's keys on its own.
/// </summary>
// See ADR-1404.
[PublicAPI]
public static class TenantKeyAuditEventNames
{
    /// <summary>
    /// The prefix of every tenant key event; the change (<see cref="TenantKeyChanges"/>) follows it.
    /// </summary>
    public const string Prefix = "tenant.key";

    /// <summary>
    /// Gets the name of the event that records the specified change, such as <c>tenant.key.generated</c>.
    /// </summary>
    /// <param name="change">The change: one of the values in <see cref="TenantKeyChanges"/>.</param>
    /// <returns>The event name.</returns>
    public static string For(string change) => $"{Prefix}.{change}";
}
