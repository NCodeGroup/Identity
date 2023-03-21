#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Messages.Parsers;

/// <summary>
/// Provides the ability to parse and load JSON into a <see cref="Parameter"/> given an <see cref="Utf8JsonReader"/>.
/// </summary>
public interface IJsonParser
{
    /// <summary>
    /// Parses and loads JSON into a <see cref="Parameter"/> given an <see cref="Utf8JsonReader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="context">The <see cref="IOpenIdContext"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to parse.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    Parameter Read(
        ref Utf8JsonReader reader,
        IOpenIdContext context,
        ParameterDescriptor descriptor,
        JsonSerializerOptions options);

    /// <summary>
    /// Serializes the JSON value from a <see cref="Parameter"/> into the given <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="context">The <see cref="IOpenIdContext"/> to use when serializing the value.</param>
    /// <param name="parameter">The <see cref="Parameter"/> to serialize as JSON.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
    void Write(
        Utf8JsonWriter writer,
        IOpenIdContext context,
        Parameter parameter,
        JsonSerializerOptions options);
}

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse JSON payloads.
/// </summary>
/// <typeparam name="T">The type of object to parse from JSON.</typeparam>
public class JsonParser<T> : ParameterParser<T?>, IJsonParser
{
    /// <summary>
    /// Gets the <see cref="JsonConverter{T}"/> that is used to (de)serialize the JSON payload.
    /// The default implementation retrieves the <see cref="JsonConverter{T}"/> from the <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="context">The <see cref="IOpenIdContext"/> to use when parsing the value.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to be parsed.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
    /// <returns>The <see cref="JsonConverter{T}"/> to (de)serialize the JSON payload.</returns>
    protected virtual JsonConverter<T?> GetJsonConverter(
        IOpenIdContext context,
        ParameterDescriptor descriptor,
        JsonSerializerOptions options
    ) => (JsonConverter<T?>)options.GetConverter(typeof(T));

    /// <inheritdoc/>
    public Parameter Read(
        ref Utf8JsonReader reader,
        IOpenIdContext context,
        ParameterDescriptor descriptor,
        JsonSerializerOptions options)
    {
        var converter = GetJsonConverter(context, descriptor, options);
        var parsedValue = converter.Read(ref reader, typeof(T), options);
        var stringValues = JsonSerializer.Serialize(parsedValue, options);
        return new Parameter<T>(descriptor, stringValues, parsedValue);
    }

    // TODO: unit tests for Write

    /// <inheritdoc/>
    public void Write(
        Utf8JsonWriter writer,
        IOpenIdContext context,
        Parameter parameter,
        JsonSerializerOptions options)
    {
        var descriptor = parameter.Descriptor;
        writer.WritePropertyName(descriptor.ParameterName);

        var typedParameter = (Parameter<T>)parameter;
        var converter = GetJsonConverter(context, descriptor, options);

        converter.Write(writer, typedParameter.ParsedValue, options);
    }

    /// <inheritdoc/>
    public override StringValues Serialize(IOpenIdContext context, T? value)
    {
        return JsonSerializer.Serialize(value, context.JsonSerializerOptions);
    }

    /// <inheritdoc/>
    public override T? Parse(
        IOpenIdContext context,
        ParameterDescriptor descriptor,
        StringValues stringValues,
        bool ignoreErrors = false)
    {
        Debug.Assert(!descriptor.AllowMultipleValues);

        switch (stringValues.Count)
        {
            case 0 when descriptor.Optional || ignoreErrors:
                return default;

            case 0:
                throw context.ErrorFactory.MissingParameter(descriptor.ParameterName).AsException();

            case > 1 when !ignoreErrors:
                throw context.ErrorFactory.TooManyParameterValues(descriptor.ParameterName).AsException();
        }

        var json = stringValues[0];

        try
        {
            return JsonSerializer.Deserialize<T>(json, context.JsonSerializerOptions);
        }
        catch (Exception exception)
        {
            if (ignoreErrors) return default;
            throw context.ErrorFactory.FailedToDeserializeJson(OpenIdConstants.ErrorCodes.InvalidRequest).WithException(exception).AsException();
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
        IOpenIdContext context,
        ParameterDescriptor descriptor,
        JsonSerializerOptions options
    ) => new TConverter();
}
