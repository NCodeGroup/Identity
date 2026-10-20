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

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Validates the keys every tenant is provisioned with when the host starts, so an algorithm that is not registered, a
/// symmetric algorithm, or a key size the algorithm does not accept stops the host instead of failing the first tenant
/// that is seeded.
/// </summary>
// See ADR-0137 (configuration is validated at startup) and ADR-0169 (every tenant is provisioned with persisted keys).
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
