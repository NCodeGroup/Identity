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

using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class SingleOrDefaultStringParser : OpenIdParameterParser<StringSegment>
    {
        public override OpenIdStringValues Serialize(StringSegment value)
        {
            const bool tokenize = false;
            return new OpenIdStringValues(value, tokenize);
        }

        public override bool TryParse(string parameterName, OpenIdStringValues stringValues, out ValidationResult<StringSegment> result)
        {
            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success<StringSegment>(null);
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues<StringSegment>(parameterName);
                    return false;

                default:
                    result = ValidationResult.Factory.Success(stringValues[0]);
                    return true;
            }
        }
    }
}
