// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Management;

/// <summary>
/// Provides constants for built-in role names used in the authorization system.
/// </summary>
public static class BuiltInRoles
{
    /// <summary>
    /// The role name for global administrators who have full access across all tenants and resources.
    /// </summary>
    public const string GlobalAdmin = "GlobalAdmin";

    /// <summary>
    /// The role name for team administrators who have full authority over a team and everything beneath it, including
    /// creating its tenants.
    /// </summary>
    // See ADR-1411
    public const string TeamAdmin = "TeamAdmin";

    /// <summary>
    /// The role name for tenant administrators who have administrative access within a specific tenant.
    /// </summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>
    /// The role name for the owner of a single resource instance, who may manage that instance (and everything beneath
    /// it) but not create new resources.
    /// </summary>
    public const string Owner = "Owner";
}
