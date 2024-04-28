#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Text.Json;
using NCode.Buffers;

namespace NCode.Identity.Jose;

/// <summary>
/// Represents a Json Web Token (JWT) in compact form with support for either JWS or JWE protection.
/// </summary>
public abstract class CompactJwt
{
    /// <summary>
    /// Gets a value indicating how the JWT is protected, either 'JWS' or 'JWE'.
    /// </summary>
    public abstract string ProtectionType { get; }

    /// <summary>
    /// Gets the segment collection from the JWT.
    /// </summary>
    public abstract StringSegments Segments { get; }

    /// <summary>
    /// Gets the encoded header from the JWT.
    /// </summary>
    public abstract ReadOnlySpan<char> EncodedHeader { get; }

    /// <summary>
    /// Gets the deserialized header from the JWT.
    /// </summary>
    public abstract JsonElement DeserializedHeader { get; }

    /// <summary>
    /// Returns the original Json Web Token (JWT) in compact form.
    /// </summary>
    public override string ToString() => Segments.Original.ToString();
}
