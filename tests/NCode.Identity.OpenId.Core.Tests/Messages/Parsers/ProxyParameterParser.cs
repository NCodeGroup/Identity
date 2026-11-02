// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parsers;

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
