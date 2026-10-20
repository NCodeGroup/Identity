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

namespace NCode.Identity.Server;

/// <summary>
/// Specifies how the OpenID server's signing keys are provided during development. Every mode except
/// <see cref="None"/> is development-only and must never be used in production.
/// </summary>
[PublicAPI]
public enum DeveloperKeyMode
{
    /// <summary>
    /// Ephemeral, in-memory signing keys that are regenerated whenever the tenant is (re)materialized and never
    /// persisted; tenants are not provisioned with persisted keys. Hermetic and dependency-free; ideal for tests.
    /// </summary>
    Ephemeral = 0,

    /// <summary>
    /// The persisted keys every tenant is provisioned with, surviving restarts when paired with a persistent database
    /// and data-protection key ring, plus development-only self-healing: a key the key ring can no longer unprotect is
    /// removed and provisioned afresh instead of failing the tenant. Intended for local running.
    /// </summary>
    PersistentSigningKey = 1,

    /// <summary>
    /// No developer keys at all: tenants hold the persisted keys they are provisioned with, or keys an operator
    /// imports, exactly as in production, and a key that can no longer be unprotected fails its tenant. This is the
    /// only mode that is safe to expose publicly, and the one a Production deployment must select.
    /// </summary>
    /// <remarks>
    /// Deliberately not the zero value. The default must stay <see cref="Ephemeral"/> so a developer who has
    /// configured nothing still gets a working, hermetic, zero-setup host with no database footprint for keys.
    /// Production states its intent explicitly rather than inheriting it.
    /// </remarks>
    None = 2,
}
