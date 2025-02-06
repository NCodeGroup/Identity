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
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Claims;

/// <summary>
/// Provides an abstraction for serializing and deserializing <see cref="Claim"/>, <see cref="ClaimsIdentity"/>,
/// and <see cref="ClaimsPrincipal"/> instances.
/// </summary>
[PublicAPI]
public interface IClaimsSerializer
{
    /// <summary>
    /// Serializes the specified <paramref name="claim"/> into a <see cref="SerializableClaim"/>.
    /// </summary>
    /// <param name="claim">The claim to serialize.</param>
    /// <returns>The serialized claim.</returns>
    SerializableClaim SerializeClaim(Claim claim);

    /// <summary>
    /// Serializes the specified <paramref name="identity"/> into a <see cref="SerializableClaimsIdentity"/>.
    /// </summary>
    /// <param name="identity">The identity to serialize.</param>
    /// <returns>The serialized identity.</returns>
    SerializableClaimsIdentity SerializeIdentity(ClaimsIdentity identity);

    /// <summary>
    /// Serializes the specified <paramref name="principal"/> into a <see cref="SerializableClaimsPrincipal"/>.
    /// </summary>
    /// <param name="principal">The principal to serialize.</param>
    /// <returns>The serialized principal.</returns>
    SerializableClaimsPrincipal SerializePrincipal(ClaimsPrincipal principal);

    /// <summary>
    /// Deserializes the specified <paramref name="serializableClaim"/> into a <see cref="Claim"/>.
    /// </summary>
    /// <param name="serializableClaim">The serialized claim to deserialize.</param>
    /// <returns>The deserialized claim.</returns>
    Claim DeserializeClaim(SerializableClaim serializableClaim);

    /// <summary>
    /// Deserializes the specified <paramref name="serializableClaim"/> into a <see cref="Claim"/>.
    /// </summary>
    /// <param name="serializableClaim">The serialized claim to deserialize.</param>
    /// <param name="subject">The optional subject identity for the claim.</param>
    /// <returns>The deserialized claim.</returns>
    Claim DeserializeClaim(SerializableClaim serializableClaim, ClaimsIdentity? subject);

    /// <summary>
    /// Deserializes the specified <paramref name="serializableIdentity"/> into a <see cref="ClaimsIdentity"/>.
    /// </summary>
    /// <param name="serializableIdentity">The serialized identity to deserialize.</param>
    /// <returns>The deserialized identity.</returns>
    ClaimsIdentity DeserializeIdentity(SerializableClaimsIdentity serializableIdentity);

    /// <summary>
    /// Deserializes the specified <paramref name="serializablePrincipal"/> into a <see cref="ClaimsPrincipal"/>.
    /// </summary>
    /// <param name="serializablePrincipal">The serialized principal to deserialize.</param>
    /// <returns>The deserialized principal.</returns>
    ClaimsPrincipal DeserializePrincipal(SerializableClaimsPrincipal serializablePrincipal);
}
