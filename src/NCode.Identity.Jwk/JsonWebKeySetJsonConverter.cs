// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NCode.Identity.Jwk;

/// <summary>
/// Serializes a <see cref="JsonWebKeySet"/> to and from its RFC 7517 §5 JSON document form (<c>{ "keys": [ … ] }</c>).
/// </summary>
internal sealed class JsonWebKeySetJsonConverter : JsonConverter<JsonWebKeySet>
{
    /// <inheritdoc />
    public override JsonWebKeySet Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => new(JsonElement.ParseValue(ref reader));

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        JsonWebKeySet value,
        JsonSerializerOptions options
    ) => value.WriteTo(writer);
}
