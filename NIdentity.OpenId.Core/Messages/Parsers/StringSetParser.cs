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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class StringSetParser : ParameterParser<IEnumerable<string>>
    {
        public override StringValues Serialize(IEnumerable<string> value)
        {
            return string.Join(Separator, value);
        }

        public override bool TryParse(ParameterDescriptor descriptor, StringValues stringValues, out ValidationResult<IEnumerable<string>> result)
        {
            switch (stringValues.Count)
            {
                case 0 when descriptor.Optional:
                    result = ValidationResult.Factory.Success(Enumerable.Empty<string>());
                    return true;

                case 0:
                    result = ValidationResult.Factory.MissingParameter<IEnumerable<string>>(descriptor.ParameterName);
                    return false;

                case > 1 when descriptor.AllowMultipleValues:
                    result = ValidationResult.Factory.Success<IEnumerable<string>>(stringValues
                        .SelectMany(stringValue => stringValue.Split(Separator))
                        .ToHashSet(StringComparer.Ordinal));
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues<IEnumerable<string>>(descriptor.ParameterName);
                    return false;

                default:
                    result = ValidationResult.Factory.Success<IEnumerable<string>>(stringValues[0]
                        .Split(Separator)
                        .ToHashSet(StringComparer.Ordinal));
                    return true;
            }
        }
    }
}
