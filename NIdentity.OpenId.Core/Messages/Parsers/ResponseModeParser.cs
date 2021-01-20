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

using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class ResponseModeParser : OpenIdParameterParser<ResponseMode?>
    {
        public override OpenIdStringValues Serialize(ResponseMode? value)
        {
            const bool tokenize = false;
            return value switch
            {
                ResponseMode.Query => new OpenIdStringValues(OpenIdConstants.ResponseModes.Query, tokenize),
                ResponseMode.Fragment => new OpenIdStringValues(OpenIdConstants.ResponseModes.Fragment, tokenize),
                ResponseMode.FormPost => new OpenIdStringValues(OpenIdConstants.ResponseModes.FormPost, tokenize),
                _ => OpenIdStringValues.Empty
            };
        }

        public override bool TryParse(string parameterName, OpenIdStringValues stringValues, out ValidationResult<ResponseMode?> result)
        {
            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success<ResponseMode?>(null);
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues<ResponseMode?>(parameterName);
                    return false;
            }

            var stringSegment = stringValues[0];

            if (stringSegment.Equals(OpenIdConstants.ResponseModes.Query, StringComparison))
            {
                result = ValidationResult.Factory.Success<ResponseMode?>(ResponseMode.Query);
                return true;
            }

            if (stringSegment.Equals(OpenIdConstants.ResponseModes.Fragment, StringComparison))
            {
                result = ValidationResult.Factory.Success<ResponseMode?>(ResponseMode.Fragment);
                return true;
            }

            if (stringSegment.Equals(OpenIdConstants.ResponseModes.FormPost, StringComparison))
            {
                result = ValidationResult.Factory.Success<ResponseMode?>(ResponseMode.FormPost);
                return true;
            }

            result = ValidationResult.Factory.InvalidParameterValue<ResponseMode?>(parameterName);
            return false;
        }
    }
}
