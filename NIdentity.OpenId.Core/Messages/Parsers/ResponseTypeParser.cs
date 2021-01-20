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
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class ResponseTypeParser : OpenIdParameterParser<ResponseTypes>
    {
        public override OpenIdStringValues Serialize(ResponseTypes value)
        {
            const int capacity = 3;
            var list = new List<string>(capacity);

            if (value.HasFlag(ResponseTypes.Code))
                list.Add(OpenIdConstants.ResponseTypes.Code);

            if (value.HasFlag(ResponseTypes.IdToken))
                list.Add(OpenIdConstants.ResponseTypes.IdToken);

            if (value.HasFlag(ResponseTypes.Token))
                list.Add(OpenIdConstants.ResponseTypes.Token);

            const bool tokenize = false;
            return new OpenIdStringValues(list, tokenize);
        }

        public override bool TryParse(string parameterName, OpenIdStringValues stringValues, out ValidationResult<ResponseTypes> result)
        {
            if (stringValues.Count == 0)
            {
                result = ValidationResult.Factory.MissingParameter<ResponseTypes>(parameterName);
                return false;
            }

            var responseType = ResponseTypes.Unknown;
            foreach (var stringValue in stringValues)
            {
                if (stringValue.Equals(OpenIdConstants.ResponseTypes.Code, StringComparison))
                {
                    responseType |= ResponseTypes.Code;
                }
                else if (stringValue.Equals(OpenIdConstants.ResponseTypes.IdToken, StringComparison))
                {
                    responseType |= ResponseTypes.IdToken;
                }
                else if (stringValue.Equals(OpenIdConstants.ResponseTypes.Token, StringComparison))
                {
                    responseType |= ResponseTypes.Token;
                }
                else
                {
                    result = ValidationResult.Factory.InvalidParameterValue<ResponseTypes>(parameterName);
                    return false;
                }
            }

            result = ValidationResult.Factory.Success(responseType);
            return true;
        }
    }
}
