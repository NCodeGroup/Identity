// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that can parse <see cref="bool"/> values.
/// </summary>
[PublicAPI]
public class BoolParser : ParameterParser<bool>
{
    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        bool parsedValue
    ) => parsedValue ? "true" : "false";

    /// <inheritdoc/>
    public override bool Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        switch (stringValues.Count)
        {
            case 0 when descriptor.AllowMissingStringValues:
                return false;

            case 0:
                throw openIdEnvironment
                    .ErrorFactory.MissingParameter(descriptor.ParameterName)
                    .AsException();

            case > 1:
                throw openIdEnvironment
                    .ErrorFactory.TooManyParameterValues(descriptor.ParameterName)
                    .AsException();
        }

        var stringValue = stringValues[0];
        Debug.Assert(stringValue is not null);

        if (!bool.TryParse(stringValue, out var parsedValue))
        {
            throw openIdEnvironment
                .ErrorFactory.InvalidParameterValue(descriptor.ParameterName)
                .AsException();
        }

        return parsedValue;
    }
}
