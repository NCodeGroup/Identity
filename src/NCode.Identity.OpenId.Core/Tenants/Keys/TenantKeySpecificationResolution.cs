// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Contains the keys every tenant is provisioned with, resolved from the configured provisioning options, and the
/// reasons any configured key cannot be provisioned.
/// </summary>
internal sealed class TenantKeySpecificationResolution
{
    /// <summary>
    /// Gets the keys every tenant is provisioned with; empty when provisioning is disabled.
    /// </summary>
    public required List<TenantKeySpecification> Specifications { get; init; }

    /// <summary>
    /// Gets a human-readable reason for every configured key that cannot be provisioned; empty when every key can be.
    /// </summary>
    public required List<string> Errors { get; init; }
}
