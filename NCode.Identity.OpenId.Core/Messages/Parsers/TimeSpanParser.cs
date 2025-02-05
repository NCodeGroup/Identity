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

using System.Globalization;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that can parse <see cref="TimeSpan"/> values.
/// </summary>
public class TimeSpanParser : ParameterParser<TimeSpan?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        TimeSpan? parsedValue)
    {
        if (parsedValue == null)
            return StringValues.Empty;

        var wholeSeconds = (int)parsedValue.Value.TotalSeconds;
        return wholeSeconds.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public override TimeSpan? Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues)
    {
        switch (stringValues.Count)
        {
            case 0 when descriptor.AllowMissingStringValues:
                return null;

            case 0:
                throw openIdEnvironment
                    .ErrorFactory
                    .MissingParameter(descriptor.ParameterName)
                    .AsException();

            case > 1 when !descriptor.AllowMultipleStringValues:
                throw openIdEnvironment
                    .ErrorFactory
                    .TooManyParameterValues(descriptor.ParameterName)
                    .AsException();
        }

        var value = TimeSpan.Zero;
        foreach (var stringValue in stringValues)
        {
            if (int.TryParse(stringValue, out var seconds))
            {
                value += TimeSpan.FromSeconds(seconds);
            }
            else
            {
                throw openIdEnvironment
                    .ErrorFactory
                    .InvalidParameterValue(descriptor.ParameterName)
                    .AsException();
            }
        }

        return value;
    }
}
