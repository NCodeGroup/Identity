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
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class TimeSpanParser : ParameterParser<TimeSpan?>
    {
        public override StringValues Serialize(IOpenIdMessageContext context, TimeSpan? value)
        {
            if (value == null)
                return StringValues.Empty;

            var wholeSeconds = (int)value.Value.TotalSeconds;
            return wholeSeconds.ToString(CultureInfo.InvariantCulture);
        }

        public override bool TryParse(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues, out ValidationResult<TimeSpan?> result)
        {
            switch (stringValues.Count)
            {
                case 0 when descriptor.Optional:
                    result = ValidationResult.Factory.Success<TimeSpan?>(null);
                    return true;

                case 0:
                    result = ValidationResult.Factory.MissingParameter<TimeSpan?>(descriptor.ParameterName);
                    return false;

                case > 1 when !descriptor.AllowMultipleValues:
                    result = ValidationResult.Factory.TooManyParameterValues<TimeSpan?>(descriptor.ParameterName);
                    return false;
            }

            var value = TimeSpan.Zero;
            foreach (var stringValue in stringValues)
            {
                if (!int.TryParse(stringValue, out var seconds))
                {
                    result = ValidationResult.Factory.InvalidParameterValue<TimeSpan?>(descriptor.ParameterName);
                    return false;
                }

                value += TimeSpan.FromSeconds(seconds);
            }

            result = ValidationResult.Factory.Success<TimeSpan?>(value);
            return true;
        }
    }
}
