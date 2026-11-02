// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Validates the keys every tenant is provisioned with when the host starts, so an algorithm that is not registered, a
/// symmetric algorithm, or a key size the algorithm does not accept stops the host instead of failing the first tenant
/// that is seeded.
/// </summary>
// See ADR-1137 (configuration is validated at startup) and ADR-1169 (every tenant is provisioned with persisted keys).
internal class TenantKeyProvisioningOptionsValidator(ITenantKeyPolicy tenantKeyPolicy)
    : IValidateOptions<OpenIdOptions>
{
    private ITenantKeyPolicy TenantKeyPolicy { get; } = tenantKeyPolicy;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpenIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var resolution = TenantKeyPolicy.ResolveSpecifications(options.Tenant.KeyProvisioning);

        return resolution.Errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                resolution.Errors.Select(error =>
                    $"{nameof(OpenIdOptions.Tenant)}:{nameof(OpenIdTenantOptions.KeyProvisioning)}: {error}"
                )
            );
    }
}
