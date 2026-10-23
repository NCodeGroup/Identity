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
/// Describes a stored client whose settings the effective settings of its tenant make incoherent, as an
/// <see cref="IClientCoherenceReporter"/> finds it.
/// </summary>
// See ADR-0233.
[PublicAPI]
public sealed class IncoherentClient
{
    /// <summary>
    /// Gets the identifier of the client.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the violations the registered rules find in the client's settings; empty when the settings document cannot
    /// be honored.
    /// </summary>
    public required IReadOnlyList<ClientCoherenceViolation> Violations { get; init; }

    /// <summary>
    /// Gets the reason the client's settings document cannot be honored, or <c>null</c> when it can be.
    /// </summary>
    public string? DocumentError { get; init; }
}
