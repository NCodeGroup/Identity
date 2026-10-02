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

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Contains the outcome of a federated-identity linking decision: whether a new external connection identity is
/// attached to an existing principal, and the join key to persist on the new identity.
/// </summary>
[PublicAPI]
public sealed class FederatedIdentityLinkDecision
{
    /// <summary>
    /// Gets the identifier of the existing principal that the new identity is attached to, or <c>null</c> when a new
    /// principal is provisioned instead.
    /// </summary>
    public string? LinkedPrincipalId { get; init; }

    /// <summary>
    /// Gets the join key to persist on the new identity (such as a verified email address), or <c>null</c> when no
    /// usable join key is recorded.
    /// </summary>
    public string? JoinKey { get; init; }
}
