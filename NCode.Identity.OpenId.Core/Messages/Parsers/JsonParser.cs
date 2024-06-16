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

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse JSON payloads.
/// </summary>
/// <typeparam name="T">The type of object to parse from JSON.</typeparam>
public class JsonParser<T> : ParameterParser<T?>
{
    /// <summary>
    /// Gets the <see cref="JsonConverter{T}"/> that is used to (de)serialize the JSON payload.
    /// The default implementation retrieves the <see cref="JsonConverter{T}"/> from the <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to be parsed.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
    /// <returns>The <see cref="JsonConverter{T}"/> to (de)serialize the JSON payload.</returns>
    protected virtual JsonConverter<T?> GetJsonConverter(
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        JsonSerializerOptions options
    ) => (JsonConverter<T?>)options.GetConverter(typeof(T));

    /// <inheritdoc/>
    public override Parameter Read(
        ref Utf8JsonReader reader,
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        JsonSerializerOptions options)
    {
        var converter = GetJsonConverter(openIdServer, descriptor, options);
        var parsedValue = converter.Read(ref reader, typeof(T), options);
        var stringValues = JsonSerializer.Serialize(parsedValue, options);
        return descriptor.Loader.Load(openIdServer, descriptor, stringValues, parsedValue);
    }

    // TODO: unit tests for Write

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        OpenIdServer openIdServer,
        Parameter parameter,
        JsonSerializerOptions options)
    {
        var descriptor = parameter.Descriptor;
        writer.WritePropertyName(descriptor.ParameterName);

        var typedParameter = (Parameter<T>)parameter;
        var converter = GetJsonConverter(openIdServer, descriptor, options);

        converter.Write(writer, typedParameter.ParsedValue, options);
    }

    /// <inheritdoc/>
    public override StringValues Serialize(
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        T? parsedValue)
    {
        return JsonSerializer.Serialize(parsedValue, openIdServer.JsonSerializerOptions);
    }

    /// <inheritdoc/>
    public override T? Parse(
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        StringValues stringValues)
    {
        Debug.Assert(!descriptor.AllowMultipleStringValues);

        switch (stringValues.Count)
        {
            case 0 when descriptor.Optional:
                return default;

            case 0:
                throw openIdServer.ErrorFactory.MissingParameter(descriptor.ParameterName).AsException();

            case > 1:
                throw openIdServer.ErrorFactory.TooManyParameterValues(descriptor.ParameterName).AsException();
        }

        var json = stringValues[0];
        Debug.Assert(json is not null);

        try
        {
            return JsonSerializer.Deserialize<T>(json, openIdServer.JsonSerializerOptions);
        }
        catch (Exception exception)
        {
            throw openIdServer.ErrorFactory
                .FailedToDeserializeJson(OpenIdConstants.ErrorCodes.InvalidRequest)
                .WithException(exception)
                .AsException();
        }
    }
}

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse JSON payloads
/// using a custom <see cref="JsonConverter{T}"/>.
/// </summary>
/// <typeparam name="T">The type of object to parse from JSON.</typeparam>
/// <typeparam name="TConverter">The custom <see cref="JsonConverter{T}"/> to use for JSON (de)serialization.</typeparam>
public class JsonParser<T, TConverter> : JsonParser<T>
    where TConverter : JsonConverter<T?>, new()
{
    /// <inheritdoc />
    protected override JsonConverter<T?> GetJsonConverter(
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        JsonSerializerOptions options
    ) => new TConverter();
}
