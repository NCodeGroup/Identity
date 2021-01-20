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

using System;
using System.Globalization;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class TimeSpanParser : OpenIdParameterParser<TimeSpan?>
    {
        public override OpenIdStringValues Serialize(TimeSpan? value)
        {
            if (value == null)
                return OpenIdStringValues.Empty;

            const bool tokenize = false;
            var wholeSeconds = (int)value.Value.TotalSeconds;
            var wholeSecondsAsString = wholeSeconds.ToString(CultureInfo.InvariantCulture);
            return new OpenIdStringValues(wholeSecondsAsString, tokenize);
        }

        public override bool TryParse(string parameterName, OpenIdStringValues stringValues, out ValidationResult<TimeSpan?> result)
        {
            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success<TimeSpan?>(null);
                    return false;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues<TimeSpan?>(parameterName);
                    return false;
            }

            if (!int.TryParse(stringValues[0].AsSpan(), out var seconds))
            {
                result = ValidationResult.Factory.InvalidParameterValue<TimeSpan?>(parameterName);
                return false;
            }

            result = ValidationResult.Factory.Success<TimeSpan?>(TimeSpan.FromSeconds(seconds));
            return true;
        }
    }
}
