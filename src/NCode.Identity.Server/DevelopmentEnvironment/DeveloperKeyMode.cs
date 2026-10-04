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
/// Specifies how <strong>development-only</strong> signing keys are provided to the OpenID server. Must never be
/// used in production, where a stable, securely-managed signing key is required (see ADR-0002).
/// </summary>
[PublicAPI]
public enum DeveloperKeyMode
{
    /// <summary>
    /// Ephemeral, in-memory signing keys that are regenerated whenever the tenant is (re)materialized and never
    /// persisted. Hermetic and dependency-free; ideal for tests.
    /// </summary>
    Ephemeral = 0,

    /// <summary>
    /// A persisted signing key seeded through the normal persistence path, surviving restarts when paired with a
    /// persistent database and data-protection key ring. Intended for local running.
    /// </summary>
    PersistentSigningKey = 1,
}
