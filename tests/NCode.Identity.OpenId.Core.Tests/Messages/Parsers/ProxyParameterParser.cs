#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

[PublicAPI]
internal class ProxyParameterParser<T> : ProxyParameterLoader, IParameterParser<T>
{
    public SerializeParameterDelegate<T>? SerializeCallback { get; set; }
    public ParseParameterDelegate<T>? ParseCallback { get; set; }
    public CloneParameterDelegate<T>? CloneCallback { get; set; }

    public StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        T? parsedValue
    )
    {
        if (SerializeCallback == null)
            throw new NotImplementedException();
        return SerializeCallback(openIdEnvironment, descriptor, parsedValue);
    }

    public T Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        if (ParseCallback == null)
            throw new NotImplementedException();
        return ParseCallback(openIdEnvironment, descriptor, stringValues);
    }

    [return: NotNullIfNotNull("value")]
    public T? Clone(T? value)
    {
        if (CloneCallback == null)
            throw new NotImplementedException();
        return CloneCallback(value);
    }
}
