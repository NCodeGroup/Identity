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
/// Carries the two views of a client's settings that an <see cref="IClientCoherenceRule"/> checks: the settings that
/// the client registers and the effective settings that result from them.
/// </summary>
// See ADR-0206.
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
