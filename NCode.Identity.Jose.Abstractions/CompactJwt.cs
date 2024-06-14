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
using JetBrains.Annotations;
using NCode.Buffers;

namespace NCode.Identity.Jose;

/// <summary>
/// Represents a Json Web Token (JWT) in compact form with support for either JWS or JWE protection.
/// </summary>
[PublicAPI]
public readonly struct CompactJwt
{
    private string? ProtectionTypeOrDefault { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactJwt"/> struct.
    /// </summary>
    /// <param name="protectionType">Contains a value indicating how the JWT is protected, either 'JWS' or 'JWE'.</param>
    /// <param name="segments">Contains the substrings from the JWT seperated by '.' (aka dot).</param>
    /// <param name="deserializedHeader">Contains the deserialized header from the JWT.</param>
    public CompactJwt(string protectionType, StringSegments segments, JsonElement deserializedHeader)
    {
        ProtectionTypeOrDefault = protectionType;
        Segments = segments;
        DeserializedHeader = deserializedHeader;
    }

    /// <summary>
    /// Gets a value indicating how the JWT is protected, either 'JWS' or 'JWE'.
    /// </summary>
    public string ProtectionType => ProtectionTypeOrDefault ?? string.Empty;

    /// <summary>
    /// Gets the segment collection from the JWT.
    /// </summary>
    public StringSegments Segments { get; }

    /// <summary>
    /// Gets the encoded header from the JWT.
    /// </summary>
    public ReadOnlySpan<char> EncodedHeader =>
        Segments.IsEmpty ? ReadOnlySpan<char>.Empty : Segments.First.Memory.Span;

    /// <summary>
    /// Gets the deserialized header from the JWT.
    /// </summary>
    public JsonElement DeserializedHeader { get; }

    /// <summary>
    /// Returns the original Json Web Token (JWT) in compact form.
    /// </summary>
    public override string ToString() => Segments.Original.ToString();
}
