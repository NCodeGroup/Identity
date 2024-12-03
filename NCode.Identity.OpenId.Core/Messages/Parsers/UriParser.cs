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

using System.Diagnostics;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse <see cref="Uri"/> values.
/// </summary>
public class UriParser : ParameterParser<Uri?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        Uri? parsedValue)
    {
        if (parsedValue is null)
            return StringValues.Empty;

        return parsedValue.AbsoluteUri;
    }

    /// <inheritdoc/>
    public override Uri? Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues)
    {
        Debug.Assert(!descriptor.AllowMultipleStringValues);

        switch (stringValues.Count)
        {
            case 0 when descriptor.AllowMissingStringValues:
                return null;

            case 0:
                throw openIdEnvironment
                    .ErrorFactory
                    .MissingParameter(descriptor.ParameterName)
                    .AsException();

            case > 1:
                throw openIdEnvironment
                    .ErrorFactory
                    .TooManyParameterValues(descriptor.ParameterName)
                    .AsException();
        }

        if (Uri.TryCreate(stringValues[0], UriKind.Absolute, out var uri))
            return uri;

        throw openIdEnvironment
            .ErrorFactory
            .InvalidParameterValue(descriptor.ParameterName)
            .AsException();
    }
}
