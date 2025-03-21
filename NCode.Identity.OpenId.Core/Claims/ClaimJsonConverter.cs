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
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Claims;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="Claim"/> instances
/// to and from JSON.
/// </summary>
[PublicAPI]
public class ClaimJsonConverter : JsonConverter<Claim>
{
    private IClaimsSerializer Serializer { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimJsonConverter"/> class.
    /// </summary>
    public ClaimJsonConverter()
        : this(DefaultClaimsSerializer.Singleton)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimJsonConverter"/> class with the specified <see cref="IClaimsSerializer"/>.
    /// </summary>
    /// <param name="serializer">The <see cref="IClaimsSerializer"/> to use for serialization and deserialization.</param>
    public ClaimJsonConverter(IClaimsSerializer serializer)
    {
        Serializer = serializer;
    }

    /// <inheritdoc />
    public override Claim? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var serializable = JsonSerializer.Deserialize<SerializableClaim>(ref reader, options);
        return serializable != null ? Serializer.DeserializeClaim(serializable) : null;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Claim value, JsonSerializerOptions options)
    {
        var serializable = Serializer.SerializeClaim(value);
        JsonSerializer.Serialize(writer, serializable, options);
    }
}
