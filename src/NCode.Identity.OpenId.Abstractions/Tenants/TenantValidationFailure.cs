// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Describes why a tenant cannot be served under its proposed identifier and issuer, as reported by
/// <see cref="ITenantResolver.ValidateTenantAsync"/>.
/// </summary>
[PublicAPI]
public readonly record struct TenantValidationFailure
{
    /// <summary>
    /// Gets a message describing the failure, suitable for returning to the caller that proposed the tenant.
    /// </summary>
    public required string Detail { get; init; }

    /// <summary>
    /// Gets a value indicating whether the failure is a conflict with another tenant — an address another tenant
    /// already holds — rather than a value that is invalid on its own.
    /// </summary>
    public bool IsConflict { get; init; }
}
