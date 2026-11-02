// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that can parse JSON payloads.
/// </summary>
/// <typeparam name="T">
/// The type of object to parse from JSON. If the type is an interface, then a custom <see cref="JsonConverter{T}"/>
/// must be configured within <see cref="JsonSerializerOptions"/>.
/// </typeparam>
[PublicAPI]
public class JsonParser<T> : ParameterParser<T>
{
    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        T? parsedValue
    ) => JsonSerializer.Serialize(parsedValue, openIdEnvironment.JsonSerializerOptions);

    /// <inheritdoc/>
    public override T? Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        switch (stringValues.Count)
        {
            case 0 when descriptor.AllowMissingStringValues:
                return default;

            case 0:
                throw openIdEnvironment
                    .ErrorFactory.MissingParameter(descriptor.ParameterName)
                    .AsException();

            case > 1:
                throw openIdEnvironment
                    .ErrorFactory.TooManyParameterValues(descriptor.ParameterName)
                    .AsException();
        }

        var stringValue = stringValues[0];
        Debug.Assert(stringValue is not null);

        try
        {
            return JsonSerializer.Deserialize<T>(
                stringValue,
                openIdEnvironment.JsonSerializerOptions
            );
        }
        catch (Exception exception)
        {
            throw openIdEnvironment
                .ErrorFactory.FailedToDeserializeJson(OpenIdProtocol.ErrorCodes.InvalidRequest)
                .WithException(exception)
                .AsException();
        }
    }
}
