using System;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class RequestClaimsParser : ParameterParser<RequestClaims?>
    {
        public override StringValues Serialize(RequestClaims? value)
        {
            throw new NotImplementedException();
        }

        public override bool TryParse(ParameterDescriptor descriptor, StringValues stringValues, out ValidationResult<RequestClaims?> result)
        {
            //switch (stringValues.Count)
            //{
            //    case 0:
            //        result = ValidationResult.Factory.Success<RequestClaims?>(null);
            //        return false;

            //    case > 1:
            //        result = ValidationResult.Factory.TooManyParameterValues<RequestClaims?>(parameterName);
            //        return false;
            //}

            //var stringSegment = stringValues[0];
            //var json = WebUtility.UrlDecode(stringValues[0]) ?? stringValues[0];

            throw new NotImplementedException();
        }
    }
}
