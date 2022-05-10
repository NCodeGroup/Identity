using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

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
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to parse.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    Parameter Read(ref Utf8JsonReader reader, ParameterDescriptor descriptor, JsonSerializerOptions options);

    /// <summary>
    /// Serializes the JSON value from a <see cref="Parameter"/> into the given <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="parameter">The <see cref="Parameter"/> to serialize as JSON.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
    void Write(Utf8JsonWriter writer, Parameter parameter, JsonSerializerOptions options);
}

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse JSON payloads.
/// </summary>
/// <typeparam name="T">The type of object to parse from JSON.</typeparam>
public class JsonParser<T> : ParameterParser<T?>, IJsonParser
{
    /// <inheritdoc/>
    public Parameter Read(ref Utf8JsonReader reader, ParameterDescriptor descriptor, JsonSerializerOptions options)
    {
        var converter = (JsonConverter<T>)options.GetConverter(typeof(T));
        var parsedValue = converter.Read(ref reader, typeof(T), options);
        var stringValues = JsonSerializer.Serialize(parsedValue, options);
        return new Parameter<T>(descriptor, stringValues, parsedValue);
    }

    // TODO: unit tests for Write

    /// <inheritdoc/>
    public void Write(Utf8JsonWriter writer, Parameter parameter, JsonSerializerOptions options)
    {
        var typedParameter = (Parameter<T>)parameter;
        var converter = (JsonConverter<T?>)options.GetConverter(typeof(T));
        converter.Write(writer, typedParameter.ParsedValue, options);
    }

    /// <inheritdoc/>
    public override StringValues Serialize(IOpenIdMessageContext context, T? value)
    {
        return JsonSerializer.Serialize(value, context.JsonSerializerOptions);
    }

    /// <inheritdoc/>
    public override T? Parse(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues)
    {
        Debug.Assert(!descriptor.AllowMultipleValues);

        switch (stringValues.Count)
        {
            case 0 when descriptor.Optional:
                return default;

            case 0:
                throw OpenIdException.Factory.MissingParameter(descriptor.ParameterName);

            case > 1:
                throw OpenIdException.Factory.TooManyParameterValues(descriptor.ParameterName);
        }

        var json = stringValues[0];

        try
        {
            return JsonSerializer.Deserialize<T>(json, context.JsonSerializerOptions);
        }
        catch (Exception exception)
        {
            throw OpenIdException.Factory.FailedToDeserializeJson(OpenIdConstants.ErrorCodes.InvalidRequest, exception);
        }
    }
}
