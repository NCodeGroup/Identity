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

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Validates the server settings carried by <see cref="OpenIdServerOptions.Settings"/>, so a setting name with no
/// registered descriptor, or a value that does not convert to its descriptor's type, stops the host when it starts
/// instead of being loaded without effect on the first request.
/// </summary>
// See ADR-0137 (fail at startup) and ADR-0139 (setting names are a closed vocabulary).
internal class ServerSettingsOptionsValidator(
    ISettingConfigurationReader settingConfigurationReader
) : IValidateOptions<OpenIdOptions>
{
    private ISettingConfigurationReader SettingConfigurationReader { get; } =
        settingConfigurationReader;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpenIdOptions options)
    {
        if (options.Server.Settings is not { } section)
            return ValidateOptionsResult.Success;

        var errors = SettingConfigurationReader.Validate(section);

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
