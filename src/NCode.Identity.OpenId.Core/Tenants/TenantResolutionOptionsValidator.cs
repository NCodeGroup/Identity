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

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Validates the strategy-independent members of <see cref="TenantResolutionOptions"/>, so a tenant-resolution
/// configuration that cannot work stops the host when it starts rather than failing on the first request that resolves
/// a tenant. Each tenant strategy validates its own options beside it, so this validator never needs to know which
/// strategies exist.
/// </summary>
// See ADR-0137 (fail at startup) and ADR-0160 (a tenant's address is validated wherever it is written).
internal class TenantResolutionOptionsValidator : IValidateOptions<TenantResolutionOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TenantResolutionOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrEmpty(options.StrategyCode))
            failures.Add($"{nameof(TenantResolutionOptions.StrategyCode)} is required.");

        ValidateRoot(options.Root, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    // The root tenant's address is judged by the selected strategy's validator, which knows the routes it serves; this
    // validator checks only what holds under every strategy.
    private static void ValidateRoot(RootTenantOptions? root, List<string> failures)
    {
        const string prefix = nameof(TenantResolutionOptions.Root);

        if (root is null)
        {
            failures.Add($"{prefix} is required.");
            return;
        }

        var rootTenantId = root.TenantId;
        if (string.IsNullOrEmpty(rootTenantId))
        {
            failures.Add($"{prefix}:{nameof(root.TenantId)} is required.");
        }
        else if (!TenantAddressRules.IsValidTenantId(rootTenantId))
        {
            failures.Add(
                $"{prefix}:{nameof(root.TenantId)} '{rootTenantId}' must be "
                    + $"{TenantAddressRules.TenantIdRequirement}."
            );
        }

        if (string.IsNullOrEmpty(root.DisplayName))
            failures.Add($"{prefix}:{nameof(root.DisplayName)} is required.");

        var rootIssuer = root.Issuer;
        if (
            !string.IsNullOrEmpty(rootIssuer)
            && !TenantAddressRules.TryParseIssuer(rootIssuer, out _)
        )
        {
            failures.Add(
                $"{prefix}:{nameof(root.Issuer)} '{rootIssuer}' must be "
                    + $"{TenantAddressRules.IssuerRequirement}."
            );
        }

        var rootAlias = root.MtlsEndpointAliasAuthority;
        if (
            !string.IsNullOrEmpty(rootAlias)
            && !TenantAddressRules.TryParseMtlsEndpointAliasAuthority(rootAlias, out _)
        )
        {
            failures.Add(
                $"{prefix}:{nameof(root.MtlsEndpointAliasAuthority)} '{rootAlias}' must be "
                    + $"{TenantAddressRules.MtlsEndpointAliasAuthorityRequirement}."
            );
        }
    }
}
