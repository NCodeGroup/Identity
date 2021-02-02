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

using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parameters
{
    internal class Parameter
    {
        public ParameterDescriptor Descriptor { get; }

        public StringValues StringValues { get; private set; }

        public object? ParsedValue { get; private set; }

        public Parameter(ParameterDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public virtual void Load(StringValues stringValues, object? parsedValue)
        {
            StringValues = stringValues;
            ParsedValue = parsedValue;
        }

        public virtual bool TryLoad(IOpenIdMessageContext context, StringValues stringValues, out ValidationResult result)
        {
            return Descriptor.Loader.TryLoad(context, this, stringValues, out result);
        }
    }
}
