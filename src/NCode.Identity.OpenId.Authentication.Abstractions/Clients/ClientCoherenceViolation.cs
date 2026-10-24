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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Describes a value that a client registers and that an effective setting does not permit, or a value that an
/// effective setting requires and that the client does not register, as found by an
/// <see cref="IClientCoherenceRule"/>.
/// </summary>
/// <param name="SettingName">The name of the client setting that registers the value or that is required.</param>
/// <param name="Value">The registered value that is not permitted, or empty when a required value is missing.</param>
/// <param name="GoverningSettingName">
/// The name of the effective setting that does not permit the value or that requires it.
/// </param>
// See ADR-0206.
[PublicAPI]
public readonly record struct ClientCoherenceViolation(
    string SettingName,
    string Value,
    string GoverningSettingName
)
{
    /// <summary>
    /// Gets the description of the violation, which names the settings and the values involved.
    /// </summary>
    public string Description { get; init; } =
        $"The '{SettingName}' value '{Value}' is not permitted by the effective '{GoverningSettingName}'.";

    /// <summary>
    /// Creates a violation for a value that an effective setting requires and that the client does not register.
    /// </summary>
    /// <param name="settingName">The name of the client setting that is required.</param>
    /// <param name="governingSettingName">The name of the effective setting that requires the value.</param>
    /// <param name="governingValue">The value of the effective setting that requires the value.</param>
    /// <returns>The <see cref="ClientCoherenceViolation"/> with an empty <see cref="Value"/>.</returns>
    public static ClientCoherenceViolation Missing(
        string settingName,
        string governingSettingName,
        string governingValue
    ) =>
        new(settingName, string.Empty, governingSettingName)
        {
            Description =
                $"The effective '{governingSettingName}' value '{governingValue}' requires a '{settingName}' value.",
        };
}
