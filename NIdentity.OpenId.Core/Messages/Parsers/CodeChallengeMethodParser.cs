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
    internal class CodeChallengeMethodParser : OpenIdParameterParser<CodeChallengeMethod?>
    {
        public override OpenIdStringValues Serialize(CodeChallengeMethod? value)
        {
            const bool tokenize = false;
            return value switch
            {
                CodeChallengeMethod.Plain => new OpenIdStringValues(OpenIdConstants.CodeChallengeMethods.Plain, tokenize),
                CodeChallengeMethod.S256 => new OpenIdStringValues(OpenIdConstants.CodeChallengeMethods.S256, tokenize),
                _ => OpenIdStringValues.Empty
            };
        }

        public override bool TryParse(string parameterName, OpenIdStringValues stringValues, out ValidationResult<CodeChallengeMethod?> result)
        {
            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success<CodeChallengeMethod?>(null);
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues<CodeChallengeMethod?>(parameterName);
                    return false;
            }

            var stringSegment = stringValues[0];

            if (stringSegment.Equals(OpenIdConstants.CodeChallengeMethods.Plain, StringComparison))
            {
                result = ValidationResult.Factory.Success<CodeChallengeMethod?>(CodeChallengeMethod.Plain);
                return true;
            }

            if (stringSegment.Equals(OpenIdConstants.CodeChallengeMethods.S256, StringComparison))
            {
                result = ValidationResult.Factory.Success<CodeChallengeMethod?>(CodeChallengeMethod.S256);
                return true;
            }

            result = ValidationResult.Factory.InvalidParameterValue<CodeChallengeMethod?>(parameterName);
            return false;
        }
    }
}
