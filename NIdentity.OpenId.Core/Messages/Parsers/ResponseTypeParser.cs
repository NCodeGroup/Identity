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

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class ResponseTypeParser : ParameterParser<ResponseTypes?>
    {
        public override StringValues Serialize(ResponseTypes? value)
        {
            if (value is null)
                return StringValues.Empty;

            const int capacity = 3;
            var responseType = value.Value;
            var list = new List<string>(capacity);

            if (responseType.HasFlag(ResponseTypes.Code))
                list.Add(OpenIdConstants.ResponseTypes.Code);

            if (responseType.HasFlag(ResponseTypes.IdToken))
                list.Add(OpenIdConstants.ResponseTypes.IdToken);

            if (responseType.HasFlag(ResponseTypes.Token))
                list.Add(OpenIdConstants.ResponseTypes.Token);

            return string.Join(Separator, list);
        }

        public override bool TryParse(ILogger logger, ParameterDescriptor descriptor, StringValues stringValues, out ValidationResult<ResponseTypes?> result)
        {
            Debug.Assert(!descriptor.AllowMultipleValues);

            switch (stringValues.Count)
            {
                case 0 when descriptor.Optional:
                    result = ValidationResult.Factory.Success<ResponseTypes?>(null);
                    return true;

                case 0:
                    result = ValidationResult.Factory.MissingParameter<ResponseTypes?>(descriptor.ParameterName);
                    return false;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues<ResponseTypes?>(descriptor.ParameterName);
                    return false;
            }

            stringValues = stringValues[0].Split(Separator);

            var responseType = ResponseTypes.Unknown;
            foreach (var stringValue in stringValues)
            {
                if (string.Equals(stringValue, OpenIdConstants.ResponseTypes.Code, StringComparison))
                {
                    responseType |= ResponseTypes.Code;
                }
                else if (string.Equals(stringValue, OpenIdConstants.ResponseTypes.IdToken, StringComparison))
                {
                    responseType |= ResponseTypes.IdToken;
                }
                else if (string.Equals(stringValue, OpenIdConstants.ResponseTypes.Token, StringComparison))
                {
                    responseType |= ResponseTypes.Token;
                }
                else
                {
                    result = ValidationResult.Factory.InvalidParameterValue<ResponseTypes?>(descriptor.ParameterName);
                    return false;
                }
            }

            result = ValidationResult.Factory.Success<ResponseTypes?>(responseType);
            return true;
        }
    }
}
