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
using NCode.Identity.Claims;

namespace NCode.Identity.OpenId.Serialization;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="ClaimsIdentity"/>
/// instances to and from JSON.
/// </summary>
[PublicAPI]
public class ClaimsIdentityJsonConverter(IClaimsSerializer serializer) : JsonConverter<ClaimsIdentity>
{
    private IClaimsSerializer Serializer { get; } = serializer;

    /// <inheritdoc />
    public override ClaimsIdentity? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var serializable = JsonSerializer.Deserialize<SerializableClaimsIdentity>(ref reader, options);
        return serializable != null ? Serializer.DeserializeIdentity(serializable) : null;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ClaimsIdentity value, JsonSerializerOptions options)
    {
        var serializable = Serializer.SerializeIdentity(value);
        JsonSerializer.Serialize(serializable, options);
    }
}
