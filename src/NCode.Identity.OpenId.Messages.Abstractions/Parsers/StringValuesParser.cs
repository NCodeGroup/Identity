// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that can parse <see cref="StringValues"/> values.
/// </summary>
[PublicAPI]
public class StringValuesParser : ParameterParser<StringValues>
{
    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues parsedValue
    ) => parsedValue;

    /// <inheritdoc/>
    public override StringValues Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        if (stringValues.Count == 0 && !descriptor.AllowMissingStringValues)
        {
            throw openIdEnvironment
                .ErrorFactory.MissingParameter(descriptor.ParameterName)
                .AsException();
        }

        return stringValues;
    }
}
