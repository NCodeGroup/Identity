#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.OpenId.Models;

/// <summary>
/// Contains the payload of a persisted grant.
/// </summary>
/// <typeparam name="TPayload">The type of the payload for the grant.</typeparam>
[PublicAPI]
public readonly struct PersistedGrant<TPayload>
{
    /// <summary>
    /// Gets or sets the status of the grant.
    /// </summary>
    public PersistedGrantStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the identifier of the client that is associated with the grant.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets or sets the identifier of the subject that is associated with the grant.
    /// </summary>
    public string? SubjectId { get; init; }

    /// <summary>
    /// Gets or sets the payload of the grant.
    /// </summary>
    public required TPayload Payload { get; init; }
}
