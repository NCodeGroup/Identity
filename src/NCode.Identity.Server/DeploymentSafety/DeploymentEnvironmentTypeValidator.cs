// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;

namespace NCode.Identity.Server.DeploymentSafety;

/// <summary>
/// Refuses a deployment (a host without <c>LocalDevelopment:Enabled</c>) whose configured platform settings select a
/// non-production environment type, because the platform-wide type is the default of every tenant that declares none,
/// so it would silently drop their production guardrails. A development overlay copied into a deployment is the case
/// this catches.
/// </summary>
/// <remarks>
/// The registered <see cref="ITenantEnvironmentType"/> strategies decide what is production, so a type a package adds is
/// judged by its own <see cref="ITenantEnvironmentType.IsProduction"/>. A value that names no registered type is left to
/// the platform settings validation, which refuses it in every environment.
/// </remarks>
// See ADR-1289.
internal class DeploymentEnvironmentTypeValidator(IEnumerable<ITenantEnvironmentType> types)
    : IValidateOptions<OpenIdOptions>
{
    /// <summary>
    /// The configuration key that the message names.
    /// </summary>
    internal const string ConfigurationKey =
        $"OpenId:Platform:Settings:{OpenIdSettingNames.EnvironmentType}";

    private IEnumerable<ITenantEnvironmentType> Types { get; } = types;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpenIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var typeName = options.Platform.Settings?[OpenIdSettingNames.EnvironmentType];
        if (string.IsNullOrEmpty(typeName))
            return ValidateOptionsResult.Success;

        var type = Types.FirstOrDefault(candidate =>
            string.Equals(candidate.TypeName, typeName, StringComparison.Ordinal)
        );
        if (type is null || type.IsProduction)
            return ValidateOptionsResult.Success;

        return ValidateOptionsResult.Fail(
            $"Refusing to start a deployment: '{ConfigurationKey}' is '{typeName}', a non-production "
                + "environment type. It is the default of every tenant that declares no type of its own, so it would "
                + "drop their production guardrails. Remove it from this deployment's configuration, and declare a "
                + "non-production type on each tenant that needs one. A host for local development sets "
                + "'LocalDevelopment:Enabled' to true."
        );
    }
}
