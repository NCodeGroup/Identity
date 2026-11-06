// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

/// <summary>
/// Represents a team's effective settings as committed and as a write would make them.
/// </summary>
/// <param name="Current">The team's effective settings as committed.</param>
/// <param name="New">The team's effective settings as the write would make them.</param>
internal readonly record struct TeamSettingsChange(
    IReadOnlySettingCollection Current,
    IReadOnlySettingCollection New
);
