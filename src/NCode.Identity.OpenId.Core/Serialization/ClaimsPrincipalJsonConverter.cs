// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using NCode.Identity.Claims;

namespace NCode.Identity.OpenId.Serialization;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="ClaimsPrincipal"/>
/// instances to and from JSON.
/// </summary>
internal class ClaimsPrincipalJsonConverter(IClaimsSerializer serializer)
    : JsonConverter<ClaimsPrincipal>
{
    private IClaimsSerializer Serializer { get; } = serializer;

    /// <inheritdoc />
    public override ClaimsPrincipal? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var serializable = JsonSerializer.Deserialize<SerializableClaimsPrincipal>(
            ref reader,
            options
        );
        return serializable != null ? Serializer.DeserializePrincipal(serializable) : null;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        ClaimsPrincipal value,
        JsonSerializerOptions options
    )
    {
        var serializable = Serializer.SerializePrincipal(value);
        JsonSerializer.Serialize(writer, serializable, options);
    }
}
