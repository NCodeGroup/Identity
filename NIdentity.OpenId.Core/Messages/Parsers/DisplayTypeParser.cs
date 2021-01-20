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
    internal class DisplayTypeParser : OpenIdParameterParser<DisplayType?>
    {
        public override OpenIdStringValues Serialize(DisplayType? value)
        {
            const bool tokenize = false;
            return value switch
            {
                DisplayType.Page => new OpenIdStringValues(OpenIdConstants.DisplayTypes.Page, tokenize),
                DisplayType.Popup => new OpenIdStringValues(OpenIdConstants.DisplayTypes.Popup, tokenize),
                DisplayType.Touch => new OpenIdStringValues(OpenIdConstants.DisplayTypes.Touch, tokenize),
                DisplayType.Wap => new OpenIdStringValues(OpenIdConstants.DisplayTypes.Wap, tokenize),
                _ => OpenIdStringValues.Empty
            };
        }

        public override bool TryParse(string parameterName, OpenIdStringValues stringValues, out ValidationResult<DisplayType?> result)
        {
            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success<DisplayType?>(null);
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues<DisplayType?>(parameterName);
                    return false;
            }

            var stringSegment = stringValues[0];

            if (stringSegment.Equals(OpenIdConstants.DisplayTypes.Page, StringComparison))
            {
                result = ValidationResult.Factory.Success<DisplayType?>(DisplayType.Page);
                return true;
            }

            if (stringSegment.Equals(OpenIdConstants.DisplayTypes.Popup, StringComparison))
            {
                result = ValidationResult.Factory.Success<DisplayType?>(DisplayType.Popup);
                return true;
            }

            if (stringSegment.Equals(OpenIdConstants.DisplayTypes.Touch, StringComparison))
            {
                result = ValidationResult.Factory.Success<DisplayType?>(DisplayType.Touch);
                return true;
            }

            if (stringSegment.Equals(OpenIdConstants.DisplayTypes.Wap, StringComparison))
            {
                result = ValidationResult.Factory.Success<DisplayType?>(DisplayType.Wap);
                return true;
            }

            result = ValidationResult.Factory.InvalidParameterValue<DisplayType?>(parameterName);
            return false;
        }
    }
}
