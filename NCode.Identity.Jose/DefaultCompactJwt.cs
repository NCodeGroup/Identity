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
/// Provides the default implementation of the <see cref="CompactJwt"/> abstraction.
/// </summary>
public class DefaultCompactJwt : CompactJwt
{
    private JsonElement? HeaderOrNull { get; set; }

    /// <inheritdoc />
    public override string ProtectionType { get; }

    /// <inheritdoc />
    public override StringSegments Segments { get; }

    /// <inheritdoc />
    public override ReadOnlySpan<char> EncodedHeader => Segments.First.Memory.Span;

    /// <inheritdoc />
    public override JsonElement DeserializedHeader => HeaderOrNull ??= DeserializeHeader();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCompactJwt"/> class.
    /// </summary>
    /// <param name="protectionType">Contains a value indicating how the JWT is protected, either 'JWS' or 'JWE'.</param>
    /// <param name="segments">Contains the substrings from the JWT seperated by '.' (aka dot).</param>
    public DefaultCompactJwt(string protectionType, StringSegments segments)
    {
        ProtectionType = protectionType;
        Segments = segments;
    }

    private JsonElement DeserializeHeader()
    {
        using var _ = JoseSerializer.DecodeBase64Url(EncodedHeader, isSensitive: false, out var utf8Json);
        return JsonSerializer.Deserialize<JsonElement>(utf8Json);
    }
}
