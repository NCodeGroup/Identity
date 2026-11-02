// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Carries the two views of a client's settings that an <see cref="IClientCoherenceRule"/> checks: the settings that
/// the client registers and the effective settings that result from them.
/// </summary>
// See ADR-1206.
[PublicAPI]
public sealed class ClientCoherenceContext
{
    /// <summary>
    /// Gets the settings the client's own settings document registers, as written: without its tenant's settings and
    /// without the ceilings or defaults of the security profiles it selects.
    /// </summary>
    public required IReadOnlySettingCollection RegisteredSettings { get; init; }

    /// <summary>
    /// Gets the effective settings of the client: its registered settings merged onto its tenant's effective settings,
    /// with the ceilings and defaults of every security profile it selects applied.
    /// </summary>
    public required IReadOnlySettingCollection EffectiveSettings { get; init; }
}
