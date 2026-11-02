// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using NCode.Identity.Claims;

namespace NCode.Identity.OpenId.Serialization;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="Claim"/> instances
/// to and from JSON.
/// </summary>
internal class ClaimJsonConverter(IClaimsSerializer serializer) : JsonConverter<Claim>
{
    private IClaimsSerializer Serializer { get; } = serializer;

    /// <inheritdoc />
    public override Claim? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
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
