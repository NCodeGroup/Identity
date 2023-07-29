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
using NCode.Jose.Exceptions;

namespace NCode.Jose;

/// <summary>
/// Represents a Json Web Token (JWT) in compact form with support for either JWS or JWE protection.
/// </summary>
public class CompactToken
{
    private JoseOptions JoseOptions { get; }
    private IReadOnlyDictionary<string, object>? DeserializedHeaderOrNull { get; set; }

    /// <summary>
    /// Gets a value indicating how the JWT is protected, either 'JWS' or 'JWE'.
    /// </summary>
    public string ProtectionType { get; }

    /// <summary>
    /// Gets the segments from the JWT.
    /// </summary>
    public StringSegments Segments { get; }

    /// <summary>
    /// Gets the encoded header from the JWT.
    /// </summary>
    public ReadOnlySpan<char> EncodedHeader => Segments.First.Memory.Span;

    /// <summary>
    /// Gets the deserialized header from the JWT.
    /// </summary>
    public IReadOnlyDictionary<string, object> DeserializedHeader => DeserializedHeaderOrNull ??= DeserializeHeader();

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactToken"/> class.
    /// </summary>
    /// <param name="protectionType">Contains a value indicating how the JWT is protected, either 'JWS' or 'JWE'.</param>
    /// <param name="segments">Contains the substrings from the JWT seperated by '.' (aka dot).</param>
    /// <param name="joseOptions">Contains the options for JOSE serialization.</param>
    public CompactToken(string protectionType, StringSegments segments, JoseOptions joseOptions)
    {
        ProtectionType = protectionType;
        Segments = segments;
        JoseOptions = joseOptions;
    }

    private IReadOnlyDictionary<string, object> DeserializeHeader()
    {
        var name = $"{ProtectionType} Protected Header";
        using var lease = JoseSerializer.DecodeBase64Url(name, EncodedHeader, isSensitive: false, out var utf8Json);
        var header = JsonSerializer.Deserialize<Dictionary<string, object>>(utf8Json, JoseOptions.JsonSerializerOptions);
        return header ?? throw new JoseException($"Failed to deserialize {name}");
    }
}
