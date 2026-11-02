// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that can parse <see cref="string"/> values.
/// </summary>
[PublicAPI]
public class StringParser : ParameterParser<string>
{
    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        string? parsedValue
    ) => parsedValue;

    /// <inheritdoc/>
    public override string? Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        // ReSharper disable once ConvertSwitchStatementToSwitchExpression
        // That makes the code unreadable.
        switch (stringValues.Count)
        {
            case 0 when descriptor.AllowMissingStringValues:
                return null;

            case 0:
                throw openIdEnvironment
                    .ErrorFactory.MissingParameter(descriptor.ParameterName)
                    .AsException();

            case > 1:
                throw openIdEnvironment
                    .ErrorFactory.TooManyParameterValues(descriptor.ParameterName)
                    .AsException();
        }

        return stringValues[0];
    }
}
