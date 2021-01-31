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
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal abstract class ParameterParser<T> : ParameterLoader
    {
        public virtual string Separator => OpenIdConstants.ParameterSeparator;

        public virtual StringComparison StringComparison => StringComparison.Ordinal;

        public abstract StringValues Serialize(IOpenIdMessageContext context, T value);

        public abstract bool TryParse(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues, out ValidationResult<T> result);

        public override bool TryLoad(IOpenIdMessageContext context, Parameter parameter, StringValues stringValues, out ValidationResult result)
        {
            if (!TryParse(context, parameter.Descriptor, stringValues, out var parseResult))
            {
                parameter.Update(stringValues, null);
                result = parseResult;
                return false;
            }

            parameter.Update(stringValues, parseResult.Value);
            result = ValidationResult.SuccessResult;
            return true;
        }
    }
}
