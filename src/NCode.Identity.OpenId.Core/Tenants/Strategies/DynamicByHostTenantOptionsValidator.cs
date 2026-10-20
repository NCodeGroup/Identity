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

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Tenants.Strategies;

/// <summary>
/// Validates <see cref="TenantResolutionOptions.DynamicByHost"/> when the host-based strategy is selected: a literal
/// tenant path and a host pattern that compiles. The options of an unselected strategy are inert, and a layered
/// configuration may legitimately carry them, so they are skipped.
/// </summary>
// See ADR-0137 (fail at startup).
internal class DynamicByHostTenantOptionsValidator : IValidateOptions<TenantResolutionOptions>
{
    private const string Prefix = OpenIdConstants.TenantStrategyCodes.DynamicByHost;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TenantResolutionOptions options)
    {
        if (!string.Equals(options.StrategyCode, Prefix, StringComparison.Ordinal))
            return ValidateOptionsResult.Skip;

        var dynamicByHost = options.DynamicByHost ?? new DynamicByHostTenantOptions();
        var failures = new List<string>();

        TenantAddressRules.TryParseLiteralPath(
            dynamicByHost.TenantPath,
            $"{Prefix}:{nameof(dynamicByHost.TenantPath)}",
            failures
        );

        try
        {
            _ = new Regex(
                dynamicByHost.RegexPattern,
                RegexOptions.CultureInvariant | RegexOptions.Singleline
            );
        }
        catch (ArgumentException exception)
        {
            failures.Add(
                $"{Prefix}:{nameof(dynamicByHost.RegexPattern)} '{dynamicByHost.RegexPattern}' is not a valid "
                    + $"regular expression: {exception.Message}"
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
