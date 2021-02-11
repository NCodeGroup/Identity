using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    public interface IJsonParser
    {
        Parameter Load(IOpenIdMessageContext context, ParameterDescriptor descriptor, ref Utf8JsonReader reader, JsonSerializerOptions options);
    }

    public class JsonParser<T> : ParameterParser<T?>, IJsonParser
    {
        public Parameter Load(IOpenIdMessageContext context, ParameterDescriptor descriptor, ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var converter = (JsonConverter<T>)options.GetConverter(typeof(T));
            var parsedValue = converter.Read(ref reader, typeof(T), options);
            var stringValues = JsonSerializer.Serialize(parsedValue, options);
            return new Parameter(descriptor, stringValues, parsedValue);
        }

        public override StringValues Serialize(IOpenIdMessageContext context, T? value)
        {
            return JsonSerializer.Serialize(value, context.JsonSerializerOptions);
        }

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
                var value = JsonSerializer.Deserialize<T>(json, context.JsonSerializerOptions);
                return value;
            }
            catch (Exception innerException)
            {
                var exception = OpenIdException.Factory.FailedToDeserializeJson(OpenIdConstants.ErrorCodes.InvalidRequest, innerException);
                context.Logger.LogError(exception, exception.ErrorDescription);
                throw exception;
            }
        }
    }
}
