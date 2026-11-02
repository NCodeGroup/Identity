// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Json;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that can parse <see cref="DateTimeOffset"/> values.
/// </summary>
public class DateTimeOffsetParser : ParameterParser<DateTimeOffset>
{
    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        DateTimeOffset parsedValue
    ) => parsedValue.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public override DateTimeOffset Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        switch (stringValues.Count)
        {
            case 0 when descriptor.AllowMissingStringValues:
                return default;

            case 0:
                throw openIdEnvironment
                    .ErrorFactory.MissingParameter(descriptor.ParameterName)
                    .AsException();

            case > 1:
                throw openIdEnvironment
                    .ErrorFactory.TooManyParameterValues(descriptor.ParameterName)
                    .AsException();
        }

        if (!DateCoercion.TryParseNumericDate(stringValues[0], out var parsedValue))
        {
            throw openIdEnvironment
                .ErrorFactory.InvalidParameterValue(descriptor.ParameterName)
                .AsException();
        }

        return parsedValue;
    }
}
