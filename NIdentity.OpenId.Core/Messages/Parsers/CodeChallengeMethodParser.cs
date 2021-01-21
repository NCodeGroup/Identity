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
    internal class CodeChallengeMethodParser : ParameterParser<CodeChallengeMethod?>
    {
        public override StringValues Serialize(CodeChallengeMethod? value)
        {
            return value switch
            {
                CodeChallengeMethod.Plain => OpenIdConstants.CodeChallengeMethods.Plain,
                CodeChallengeMethod.S256 => OpenIdConstants.CodeChallengeMethods.S256,
                _ => null
            };
        }

        public override bool TryParse(ParameterDescriptor descriptor, StringValues stringValues, out ValidationResult<CodeChallengeMethod?> result)
        {
            Debug.Assert(descriptor.Optional);
            Debug.Assert(!descriptor.AllowMultipleValues);

            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success<CodeChallengeMethod?>(null);
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues<CodeChallengeMethod?>(descriptor.ParameterName);
                    return false;
            }

            var stringValue = stringValues[0];

            if (string.Equals(stringValue, OpenIdConstants.CodeChallengeMethods.Plain, StringComparison))
            {
                result = ValidationResult.Factory.Success<CodeChallengeMethod?>(CodeChallengeMethod.Plain);
                return true;
            }

            if (string.Equals(stringValue, OpenIdConstants.CodeChallengeMethods.S256, StringComparison))
            {
                result = ValidationResult.Factory.Success<CodeChallengeMethod?>(CodeChallengeMethod.S256);
                return true;
            }

            result = ValidationResult.Factory.InvalidParameterValue<CodeChallengeMethod?>(descriptor.ParameterName);
            return false;
        }
    }
}
