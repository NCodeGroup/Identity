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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class DisplayTypeParser : ParameterParser<DisplayType?>
    {
        public override StringValues Serialize(DisplayType? value)
        {
            return value switch
            {
                DisplayType.Page => OpenIdConstants.DisplayTypes.Page,
                DisplayType.Popup => OpenIdConstants.DisplayTypes.Popup,
                DisplayType.Touch => OpenIdConstants.DisplayTypes.Touch,
                DisplayType.Wap => OpenIdConstants.DisplayTypes.Wap,
                _ => null
            };
        }

        public override bool TryParse(ILogger logger, ParameterDescriptor descriptor, StringValues stringValues, out ValidationResult<DisplayType?> result)
        {
            Debug.Assert(descriptor.Optional);
            Debug.Assert(!descriptor.AllowMultipleValues);

            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success<DisplayType?>(null);
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues<DisplayType?>(descriptor.ParameterName);
                    return false;
            }

            var stringValue = stringValues[0];

            if (string.Equals(stringValue, OpenIdConstants.DisplayTypes.Page, StringComparison))
            {
                result = ValidationResult.Factory.Success<DisplayType?>(DisplayType.Page);
                return true;
            }

            if (string.Equals(stringValue, OpenIdConstants.DisplayTypes.Popup, StringComparison))
            {
                result = ValidationResult.Factory.Success<DisplayType?>(DisplayType.Popup);
                return true;
            }

            if (string.Equals(stringValue, OpenIdConstants.DisplayTypes.Touch, StringComparison))
            {
                result = ValidationResult.Factory.Success<DisplayType?>(DisplayType.Touch);
                return true;
            }

            if (string.Equals(stringValue, OpenIdConstants.DisplayTypes.Wap, StringComparison))
            {
                result = ValidationResult.Factory.Success<DisplayType?>(DisplayType.Wap);
                return true;
            }

            result = ValidationResult.Factory.InvalidParameterValue<DisplayType?>(descriptor.ParameterName);
            return false;
        }
    }
}
