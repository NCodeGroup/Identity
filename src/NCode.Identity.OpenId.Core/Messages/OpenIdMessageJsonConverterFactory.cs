// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using NCode.Identity.OpenId.Environments;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides a <see cref="JsonConverterFactory"/> implementation that can serialize and deserialize <see cref="IOpenIdMessage"/>
/// instances to and from JSON.
/// </summary>
internal class OpenIdMessageJsonConverterFactory(OpenIdEnvironment openIdEnvironment)
    : JsonConverterFactory
{
    private OpenIdEnvironment OpenIdEnvironment { get; } = openIdEnvironment;

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(IOpenIdMessage).IsAssignableFrom(typeToConvert);
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var typeOfConverter = typeof(OpenIdMessageJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(typeOfConverter, OpenIdEnvironment);
    }
}
