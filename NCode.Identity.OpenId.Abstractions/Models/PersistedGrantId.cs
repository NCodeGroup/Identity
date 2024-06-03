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

namespace NCode.Identity.OpenId.Models;

/// <summary>
/// Contains the identifiers of a persisted grant.
/// </summary>
public readonly struct PersistedGrantId
{
    /// <summary>
    /// Gets or sets the identifier of the tenant that is associated with the grant.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the type of the grant.
    /// </summary>
    public required string GrantType { get; init; }

    /// <summary>
    /// Gets or sets the unique identifier of the grant.
    /// This value is never persisted to storage as-is, instead it's hash is used as the key.
    /// </summary>
    public required string GrantKey { get; init; }
}
