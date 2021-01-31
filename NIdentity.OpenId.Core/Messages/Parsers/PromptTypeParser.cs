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
    internal class PromptTypeParser : ParameterParser<PromptTypes?>
    {
        public override StringValues Serialize(PromptTypes? value)
        {
            if (value is null)
                return StringValues.Empty;

            var promptType = value.Value;
            if (promptType.HasFlag(PromptTypes.None))
                return OpenIdConstants.PromptTypes.None;

            const int capacity = 3;
            var list = new List<string>(capacity);

            if (promptType.HasFlag(PromptTypes.Login))
                list.Add(OpenIdConstants.PromptTypes.Login);

            if (promptType.HasFlag(PromptTypes.Consent))
                list.Add(OpenIdConstants.PromptTypes.Consent);

            if (promptType.HasFlag(PromptTypes.SelectAccount))
                list.Add(OpenIdConstants.PromptTypes.SelectAccount);

            return string.Join(Separator, list);
        }

        public override bool TryParse(ILogger logger, ParameterDescriptor descriptor, StringValues stringValues, out ValidationResult<PromptTypes?> result)
        {
            Debug.Assert(!descriptor.AllowMultipleValues);

            switch (stringValues.Count)
            {
                case 0 when descriptor.Optional:
                    result = ValidationResult.Factory.Success<PromptTypes?>(null);
                    return true;

                case 0:
                    result = ValidationResult.Factory.MissingParameter<PromptTypes?>(descriptor.ParameterName);
                    return false;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues<PromptTypes?>(descriptor.ParameterName);
                    return false;
            }

            stringValues = stringValues[0].Split(Separator);

            var promptType = PromptTypes.Unknown;
            foreach (var stringValue in stringValues)
            {
                // 'none' must be by itself
                if (promptType.HasFlag(PromptTypes.None))
                {
                    result = ValidationResult.Factory.InvalidRequest<PromptTypes?>("The none value for prompt must not be combined with other values.");
                    return false;
                }

                if (string.Equals(stringValue, OpenIdConstants.PromptTypes.None, StringComparison))
                {
                    promptType |= PromptTypes.None;
                }
                else if (string.Equals(stringValue, OpenIdConstants.PromptTypes.Login, StringComparison))
                {
                    promptType |= PromptTypes.Login;
                }
                else if (string.Equals(stringValue, OpenIdConstants.PromptTypes.Consent, StringComparison))
                {
                    promptType |= PromptTypes.Consent;
                }
                else if (string.Equals(stringValue, OpenIdConstants.PromptTypes.SelectAccount, StringComparison))
                {
                    promptType |= PromptTypes.SelectAccount;
                }
                else
                {
                    // TODO: ignore unsupported values
                    result = ValidationResult.Factory.InvalidParameterValue<PromptTypes?>(descriptor.ParameterName);
                    return false;
                }
            }

            result = ValidationResult.Factory.Success<PromptTypes?>(promptType);
            return false;
        }
    }
}
