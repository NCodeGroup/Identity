using System;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class RequestClaimsParser : OpenIdParameterParser<RequestClaims?>
    {
        public override OpenIdStringValues Serialize(RequestClaims? value)
        {
            throw new NotImplementedException();
        }

        public override bool TryParse(string parameterName, OpenIdStringValues stringValues, out ValidationResult<RequestClaims?> result)
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
