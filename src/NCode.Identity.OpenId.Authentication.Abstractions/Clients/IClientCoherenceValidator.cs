// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Checks the settings of a client against every registered <see cref="IClientCoherenceRule"/>.
/// </summary>
/// <remarks>
/// The validator reads the registered settings of a client from its settings document and resolves its effective
/// settings onto the effective settings of its tenant, so every caller gives every rule the same two views of the
/// client.
/// </remarks>
// See ADR-1206.
[PublicAPI]
public interface IClientCoherenceValidator
{
    /// <summary>
    /// Finds the violations of every registered rule in a client's settings document. The registered settings are read
    /// from the document, and the effective settings are resolved onto the effective settings of the client's tenant.
    /// </summary>
    /// <param name="tenantSettings">The effective settings of the client's tenant.</param>
    /// <param name="clientSettings">The client's own settings as a JSON document.</param>
    /// <returns>The violations of every registered rule, which is empty when the client settings are coherent.</returns>
    /// <exception cref="JsonException">The document cannot be honored, including a <c>security_profiles</c> value that
    /// names no registered profile.</exception>
    IReadOnlyList<ClientCoherenceViolation> Validate(
        IReadOnlySettingCollection tenantSettings,
        JsonElement clientSettings
    );

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
