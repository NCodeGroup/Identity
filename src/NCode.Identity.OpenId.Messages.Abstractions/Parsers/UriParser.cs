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
/// Provides an implementation of <see cref="IParameterParser{T}"/> that can parse <see cref="Uri"/> values.
/// </summary>
[PublicAPI]
public class UriParser : ParameterParser<Uri>
{
    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        Uri? parsedValue
    )
    {
        if (parsedValue is null)
            return StringValues.Empty;

        // A parameter is written as the client wrote it; AbsoluteUri would normalize it (ADR-1203).
        return parsedValue.OriginalString;
    }

    /// <inheritdoc/>
    public override Uri? Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
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

        var stringValue = stringValues[0];
        Debug.Assert(stringValue is not null);

        if (!AbsoluteUri.TryParse(stringValue, out var uri))
        {
            throw openIdEnvironment
                .ErrorFactory.InvalidParameterValue(descriptor.ParameterName)
                .AsException();
        }

        return uri;
    }
}
