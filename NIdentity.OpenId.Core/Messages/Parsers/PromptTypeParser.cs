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
    internal class PromptTypeParser : OpenIdParameterParser<PromptTypes?>
    {
        public override OpenIdStringValues Serialize(PromptTypes? value)
        {
            const bool tokenize = false;
            return value switch
            {
                PromptTypes.None => new OpenIdStringValues(OpenIdConstants.PromptTypes.None, tokenize),
                PromptTypes.Login => new OpenIdStringValues(OpenIdConstants.PromptTypes.Login, tokenize),
                PromptTypes.Consent => new OpenIdStringValues(OpenIdConstants.PromptTypes.Consent, tokenize),
                PromptTypes.SelectAccount => new OpenIdStringValues(OpenIdConstants.PromptTypes.SelectAccount, tokenize),
                _ => OpenIdStringValues.Empty
            };
        }

        public override bool TryParse(string parameterName, OpenIdStringValues stringValues, out ValidationResult<PromptTypes?> result)
        {
            if (stringValues.Count == 0)
            {
                result = ValidationResult.Factory.Success<PromptTypes?>(null);
                return true;
            }

            var promptType = PromptTypes.Unknown;
            foreach (var stringSegment in stringValues)
            {
                // 'none' must be by itself
                if (promptType.HasFlag(PromptTypes.None))
                {
                    result = ValidationResult.Factory.InvalidRequest<PromptTypes?>("The none value for prompt must not be combined with other values.");
                    return false;
                }

                if (stringSegment.Equals(OpenIdConstants.PromptTypes.None, StringComparison))
                {
                    promptType |= PromptTypes.None;
                }
                else if (stringSegment.Equals(OpenIdConstants.PromptTypes.Login, StringComparison))
                {
                    promptType |= PromptTypes.Login;
                }
                else if (stringSegment.Equals(OpenIdConstants.PromptTypes.Consent, StringComparison))
                {
                    promptType |= PromptTypes.Consent;
                }
                else if (stringSegment.Equals(OpenIdConstants.PromptTypes.SelectAccount, StringComparison))
                {
                    promptType |= PromptTypes.SelectAccount;
                }
                else
                {
                    // TODO: ignore unsupported values
                    result = ValidationResult.Factory.InvalidParameterValue<PromptTypes?>(parameterName);
                    return false;
                }
            }

            result = ValidationResult.Factory.Success<PromptTypes?>(promptType);
            return false;
        }
    }
}
