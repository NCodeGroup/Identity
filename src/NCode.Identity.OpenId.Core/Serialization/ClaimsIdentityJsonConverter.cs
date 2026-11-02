// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using NCode.Identity.Claims;

namespace NCode.Identity.OpenId.Serialization;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="ClaimsIdentity"/>
/// instances to and from JSON.
/// </summary>
internal class ClaimsIdentityJsonConverter(IClaimsSerializer serializer)
    : JsonConverter<ClaimsIdentity>
{
    private IClaimsSerializer Serializer { get; } = serializer;

    /// <inheritdoc />
    public override ClaimsIdentity? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var serializable = JsonSerializer.Deserialize<SerializableClaimsIdentity>(
            ref reader,
            options
        );
        return serializable != null ? Serializer.DeserializeIdentity(serializable) : null;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        ClaimsIdentity value,
        JsonSerializerOptions options
    )
    {
        var serializable = Serializer.SerializeIdentity(value);
        JsonSerializer.Serialize(writer, serializable, options);
    }
}
