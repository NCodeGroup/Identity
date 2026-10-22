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
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Checks the effective settings of a client against every registered <see cref="IClientCoherenceRule"/>.
/// </summary>
/// <remarks>
/// The validator works on settings that the caller has already resolved, so it has no store or resolver dependency.
/// </remarks>
// See ADR-0206.
[PublicAPI]
public interface IClientCoherenceValidator
{
    /// <summary>
    /// Finds the violations of every registered rule in the effective settings of a client.
    /// </summary>
    /// <param name="clientSettings">The effective settings of the client.</param>
    /// <returns>The violations of every registered rule, which is empty when the client settings are coherent.</returns>
    IReadOnlyList<ClientCoherenceViolation> Validate(IReadOnlySettingCollection clientSettings);

    /// <summary>
    /// Determines whether any setting that a registered rule reads has a different effective value in the current
    /// settings than in the previous settings.
    /// </summary>
    /// <remarks>
    /// A list value is compared as a set, so a change of order alone is not a change.
    /// </remarks>
    /// <param name="previousSettings">The effective settings before the change.</param>
    /// <param name="currentSettings">The effective settings after the change.</param>
    /// <returns><c>true</c> if a setting that a registered rule reads has a different effective value; otherwise, <c>false</c>.</returns>
    bool ReadsChangedSetting(
        IReadOnlySettingCollection previousSettings,
        IReadOnlySettingCollection currentSettings
    );
}
