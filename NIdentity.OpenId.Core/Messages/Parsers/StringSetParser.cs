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
using System.Linq;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal class StringSetParser : OpenIdParameterParser<IEnumerable<StringSegment>>
    {
        public override OpenIdStringValues Serialize(IEnumerable<StringSegment> value)
        {
            return new OpenIdStringValues(value);
        }

        public override bool TryParse(string parameterName, OpenIdStringValues stringValues, out ValidationResult<IEnumerable<StringSegment>> result)
        {
            result = ValidationResult.Factory.Success<IEnumerable<StringSegment>>(stringValues.ToHashSet(StringSegmentComparer.Ordinal));
            return true;
        }
    }
}
