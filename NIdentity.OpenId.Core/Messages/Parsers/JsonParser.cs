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
        public override StringValues Serialize(IOpenIdMessageContext context, T? value)
        {
            return JsonSerializer.Serialize(value, context.JsonSerializerOptions);
        }

        public override bool TryParse(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues, out ValidationResult<T?> result)
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

            try
            {
                var value = JsonSerializer.Deserialize<T>(json, context.JsonSerializerOptions);
                result = ValidationResult.Factory.Success(value);
                return true;
            }
            catch (Exception exception)
            {
                const string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest;
                result = ValidationResult.Factory.FailedToDeserializeJson<T?>(errorCode);
                context.Logger.LogError(exception, result.ErrorDetails!.ErrorDescription);
                return false;
            }
        }
    }
}
