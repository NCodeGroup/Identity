#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
/// Contains the serialized representation of a <see cref="ClaimsPrincipal"/> instance.
/// </summary>
public class SerializableClaimsPrincipal
{
    /// <summary>
    /// Gets or sets the value for the <see cref="ClaimsPrincipal.Identities"/> property.
    /// </summary>
    public required IReadOnlyCollection<SerializableClaimsIdentity> Identities { get; init; }
}
