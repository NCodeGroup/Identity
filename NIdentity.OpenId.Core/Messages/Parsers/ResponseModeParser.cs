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
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class ResponseModeParser : ParameterParser<ResponseMode?>
    {
        public override StringValues Serialize(IOpenIdMessageContext context, ResponseMode? value)
        {
            return value switch
            {
                ResponseMode.Query => OpenIdConstants.ResponseModes.Query,
                ResponseMode.Fragment => OpenIdConstants.ResponseModes.Fragment,
                ResponseMode.FormPost => OpenIdConstants.ResponseModes.FormPost,
                _ => null
            };
        }

        public override bool TryParse(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues, out ValidationResult<ResponseMode?> result)
        {
            Debug.Assert(!descriptor.AllowMultipleValues);

            switch (stringValues.Count)
            {
                case 0 when descriptor.Optional:
                    result = ValidationResult.Factory.Success<ResponseMode?>(null);
                    return true;

                case 0:
                    result = ValidationResult.Factory.MissingParameter(descriptor.ParameterName).As<ResponseMode?>();
                    return false;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues(descriptor.ParameterName).As<ResponseMode?>();
                    return false;
            }

            var stringValue = stringValues[0];

            if (string.Equals(stringValue, OpenIdConstants.ResponseModes.Query, StringComparison))
            {
                result = ValidationResult.Factory.Success<ResponseMode?>(ResponseMode.Query);
                return true;
            }

            if (string.Equals(stringValue, OpenIdConstants.ResponseModes.Fragment, StringComparison))
            {
                result = ValidationResult.Factory.Success<ResponseMode?>(ResponseMode.Fragment);
                return true;
            }

            if (string.Equals(stringValue, OpenIdConstants.ResponseModes.FormPost, StringComparison))
            {
                result = ValidationResult.Factory.Success<ResponseMode?>(ResponseMode.FormPost);
                return true;
            }

            result = ValidationResult.Factory.InvalidParameterValue(descriptor.ParameterName).As<ResponseMode?>();
            return false;
        }
    }
}
