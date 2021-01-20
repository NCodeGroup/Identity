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
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal abstract class OpenIdParameterParser<T> : OpenIdParameterLoader
    {
        public virtual StringComparison StringComparison => StringComparison.Ordinal;

        public abstract OpenIdStringValues Serialize(T value);

        public abstract bool TryParse(string parameterName, OpenIdStringValues stringValues, out ValidationResult<T> result);

        public override bool TryLoad(string parameterName, OpenIdStringValues stringValues, OpenIdParameter parameter, out ValidationResult result)
        {
            if (!TryParse(parameterName, stringValues, out var parseResult))
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
