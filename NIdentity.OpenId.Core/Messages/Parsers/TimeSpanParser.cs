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
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse <see cref="TimeSpan"/> values.
/// </summary>
public class TimeSpanParser : ParameterParser<TimeSpan?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(
        OpenIdContext context,
        TimeSpan? value)
    {
        if (value == null)
            return StringValues.Empty;

        var wholeSeconds = (int)value.Value.TotalSeconds;
        return wholeSeconds.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public override TimeSpan? Parse(
        OpenIdContext context,
        ParameterDescriptor descriptor,
        StringValues stringValues)
    {
        switch (stringValues.Count)
        {
            case 0 when descriptor.Optional:
                return null;

            case 0:
                throw context.ErrorFactory.MissingParameter(descriptor.ParameterName).AsException();

            case > 1 when !descriptor.AllowMultipleValues:
                throw context.ErrorFactory.TooManyParameterValues(descriptor.ParameterName).AsException();
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
                throw context.ErrorFactory.InvalidParameterValue(descriptor.ParameterName).AsException();
            }
        }

        return value;
    }
}
