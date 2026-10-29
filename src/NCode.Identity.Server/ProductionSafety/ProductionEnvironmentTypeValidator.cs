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
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;

namespace NCode.Identity.Server.ProductionSafety;

/// <summary>
/// Refuses a Production host whose configured server settings select a non-production environment type, because the
/// server-wide type is the default of every tenant that declares none, so it would silently drop their production
/// guardrails. A development overlay copied into a Production deployment is the case this catches.
/// </summary>
/// <remarks>
/// The registered <see cref="ITenantEnvironmentType"/> strategies decide what is production, so a type a package adds is
/// judged by its own <see cref="ITenantEnvironmentType.IsProduction"/>. A value that names no registered type is left to
/// the server settings validation, which refuses it in every environment.
/// </remarks>
// See ADR-0289.
internal class ProductionEnvironmentTypeValidator(IEnumerable<ITenantEnvironmentType> types)
    : IValidateOptions<OpenIdOptions>
{
    /// <summary>
    /// The configuration key that the message names.
    /// </summary>
    internal const string ConfigurationKey =
        $"OpenId:Server:Settings:{OpenIdSettingNames.EnvironmentType}";

    private IEnumerable<ITenantEnvironmentType> Types { get; } = types;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpenIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var typeName = options.Server.Settings?[OpenIdSettingNames.EnvironmentType];
        if (string.IsNullOrEmpty(typeName))
            return ValidateOptionsResult.Success;

        var type = Types.FirstOrDefault(candidate =>
            string.Equals(candidate.TypeName, typeName, StringComparison.Ordinal)
        );
        if (type is null || type.IsProduction)
            return ValidateOptionsResult.Success;

        return ValidateOptionsResult.Fail(
            $"Refusing to start in the Production environment: '{ConfigurationKey}' is '{typeName}', a non-production "
                + "environment type. It is the default of every tenant that declares no type of its own, so it would "
                + "drop their production guardrails. Remove it from this deployment's configuration, and declare a "
                + "non-production type on each tenant that needs one."
        );
    }
}
