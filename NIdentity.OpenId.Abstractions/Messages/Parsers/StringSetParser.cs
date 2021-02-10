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
    public class StringSetParser : ParameterParser<IReadOnlyCollection<string>?>
    {
        public override StringValues Serialize(IOpenIdMessageContext context, IReadOnlyCollection<string>? value)
        {
            if (value is null)
                return StringValues.Empty;

            if (value.Count == 0)
                return StringValues.Empty;

            return string.Join(Separator, value);
        }

        public override IReadOnlyCollection<string>? Parse(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues) => stringValues.Count switch
        {
            0 when descriptor.Optional => null,
            0 => throw OpenIdException.Factory.MissingParameter(descriptor.ParameterName),
            > 1 when descriptor.AllowMultipleValues => stringValues.SelectMany(stringValue => stringValue.Split(Separator)).ToHashSet(StringComparer.Ordinal),
            > 1 => throw OpenIdException.Factory.TooManyParameterValues(descriptor.ParameterName),
            _ => stringValues[0].Split(Separator).ToHashSet(StringComparer.Ordinal)
        };
    }
}
