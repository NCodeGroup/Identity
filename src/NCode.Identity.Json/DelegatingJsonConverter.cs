// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.Json;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize a type by delegating to another type.
/// </summary>
/// <typeparam name="TInterface">The original type to serialize and deserialize.</typeparam>
/// <typeparam name="TImplementation">The implementation type that will be used to serialize and deserialize the original type.</typeparam>
[PublicAPI]
public sealed class DelegatingJsonConverter<TInterface, TImplementation> : JsonConverter<TInterface>
    where TImplementation : TInterface, new()
{
    /// <summary>
    /// Gets or sets a value indicating whether the converter requires the type to convert to exactly match
    /// <typeparamref name="TInterface"/> or if it can be assignable from <typeparamref name="TInterface"/>.
    /// </summary>
    public bool MatchExact { get; set; } = true;

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        MatchExact
            ? typeToConvert == typeof(TInterface)
            : typeof(TInterface).IsAssignableTo(typeToConvert) && typeToConvert != typeof(object);

    /// <inheritdoc />
    public override TInterface? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => JsonSerializer.Deserialize<TImplementation>(ref reader, options);

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        TInterface value,
        JsonSerializerOptions options
    ) => JsonSerializer.Serialize(writer, value, typeof(TImplementation), options);
}
