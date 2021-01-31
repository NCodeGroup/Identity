using System;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class JsonParser<T> : ParameterParser<T?>
    {
        private readonly Action<JsonSerializerOptions> _configureOptions;

        public JsonParser()
        {
            _configureOptions = _ => { };
        }

        public JsonParser(Action<JsonSerializerOptions> configureOptions)
        {
            _configureOptions = configureOptions;
        }

        private JsonSerializerOptions GetJsonSerializerOptions()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            _configureOptions(options);
            return options;
        }

        public override StringValues Serialize(T? value)
        {
            var options = GetJsonSerializerOptions();
            return JsonSerializer.Serialize(value, options);
        }

        public override bool TryParse(ILogger logger, ParameterDescriptor descriptor, StringValues stringValues, out ValidationResult<T?> result)
        {
            Debug.Assert(!descriptor.AllowMultipleValues);

            switch (stringValues.Count)
            {
                case 0 when descriptor.Optional:
                    result = ValidationResult.Factory.Success<T?>(default);
                    return true;

                case 0:
                    result = ValidationResult.Factory.MissingParameter<T?>(descriptor.ParameterName);
                    return false;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues<T?>(descriptor.ParameterName);
                    return false;
            }

            var json = stringValues[0];

            var options = GetJsonSerializerOptions();

            try
            {
                var value = JsonSerializer.Deserialize<T>(json, options);
                result = ValidationResult.Factory.Success(value);
                return true;
            }
            catch (Exception exception)
            {
                const string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest;
                result = ValidationResult.Factory.FailedToDeserializeJson<T?>(errorCode);
                logger.LogError(exception, result.ErrorDetails!.ErrorDescription);
                return false;
            }
        }
    }
}
