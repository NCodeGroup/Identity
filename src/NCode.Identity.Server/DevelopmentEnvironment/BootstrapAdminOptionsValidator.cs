// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// Validates <see cref="BootstrapAdminOptions"/> when the bootstrap administrator is configured: it must name the one
/// tenant it is seeded into. Without that, every tenant — including each one created at runtime — would receive a client
/// whose tokens carry the <c>GlobalAdmin</c> role.
/// </summary>
// See ADR-1137 (fail at startup) and ADR-1166 (a seeder that names a tenant seeds exactly that tenant).
internal sealed class BootstrapAdminOptionsValidator : IValidateOptions<BootstrapAdminOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BootstrapAdminOptions options)
    {
        if (!options.IsEnabled || !string.IsNullOrEmpty(options.TenantId))
            return ValidateOptionsResult.Success;

        return ValidateOptionsResult.Fail(
            $"{BootstrapAdminOptions.DefaultSectionName}:{nameof(options.TenantId)} is required when the bootstrap "
                + "administrator is configured: name the one tenant its client is seeded into."
        );
    }
}
