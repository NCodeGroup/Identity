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

using System.Security.Claims;

namespace NCode.Identity.OpenId.Claims;

/// <summary>
/// Contains the serialized representation of a <see cref="ClaimsIdentity"/> instance.
/// </summary>
public class SerializableClaimsIdentity
{
    /// <summary>
    /// Gets or sets the unique reference identifier for the <see cref="ClaimsIdentity"/> instance.
    /// This value is used for <c>subject</c> references from <see cref="Claim"/>.
    /// </summary>
    public required string ReferenceId { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="ClaimsIdentity.Label"/> property.
    /// </summary>
    public required string? Label { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="ClaimsIdentity.BootstrapContext"/> property.
    /// </summary>
    public required string? BootstrapContext { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="ClaimsIdentity.AuthenticationType"/> property.
    /// </summary>
    public required string? AuthenticationType { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="ClaimsIdentity.NameClaimType"/> property.
    /// </summary>
    public required string? NameClaimType { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="ClaimsIdentity.RoleClaimType"/> property.
    /// </summary>
    public required string? RoleClaimType { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="ClaimsIdentity.Actor"/> property.
    /// </summary>
    public required SerializableClaimsIdentity? Actor { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="ClaimsIdentity.Claims"/> property.
    /// </summary>
    public required IReadOnlyCollection<SerializableClaim> Claims { get; init; }
}
