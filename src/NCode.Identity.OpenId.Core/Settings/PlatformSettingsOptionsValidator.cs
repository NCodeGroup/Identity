// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Validates the server settings carried by <see cref="OpenIdPlatformOptions.Settings"/>, so a setting name with no
/// registered descriptor, a setting that applies to no scope, a value that does not convert to its descriptor's type,
/// or a value outside the setting's domain stops the host when it starts instead of being loaded without effect on the
/// first request.
/// </summary>
// See ADR-1137 (fail at startup), ADR-1139 (setting names are a closed vocabulary), ADR-1140 (value domains), and
// ADR-1149 (scopes).
internal class PlatformSettingsOptionsValidator(
    ISettingConfigurationReader settingConfigurationReader
) : IValidateOptions<OpenIdOptions>
{
    private ISettingConfigurationReader SettingConfigurationReader { get; } =
        settingConfigurationReader;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpenIdOptions options)
    {
        if (options.Platform.Settings is not { } section)
            return ValidateOptionsResult.Success;

        var errors = SettingConfigurationReader.Validate(section, OpenIdSettingScopes.Platform);

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
