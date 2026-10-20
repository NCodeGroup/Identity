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

        var rootTenantId = options.RootTenantId;
        if (string.IsNullOrEmpty(rootTenantId))
        {
            failures.Add($"{nameof(TenantResolutionOptions.RootTenantId)} is required.");
        }
        else if (!TenantAddressRules.IsValidTenantId(rootTenantId))
        {
            failures.Add(
                $"{nameof(TenantResolutionOptions.RootTenantId)} '{rootTenantId}' must be "
                    + $"{TenantAddressRules.TenantIdRequirement}."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
