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
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse <see cref="DisplayType"/> values.
/// </summary>
public class DisplayTypeParser : ParameterParser<DisplayType?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(IOpenIdContext context, DisplayType? value)
    {
        return value switch
        {
            DisplayType.Page => OpenIdConstants.DisplayTypes.Page,
            DisplayType.Popup => OpenIdConstants.DisplayTypes.Popup,
            DisplayType.Touch => OpenIdConstants.DisplayTypes.Touch,
            DisplayType.Wap => OpenIdConstants.DisplayTypes.Wap,
            _ => null
        };
    }

    /// <inheritdoc/>
    public override DisplayType? Parse(IOpenIdContext context, ParameterDescriptor descriptor, StringValues stringValues, bool ignoreErrors = false)
    {
        Debug.Assert(!descriptor.AllowMultipleValues);

        switch (stringValues.Count)
        {
            case 0 when descriptor.Optional || ignoreErrors:
                return null;

            case 0:
                throw context.ErrorFactory.MissingParameter(descriptor.ParameterName).AsException();

            case > 1 when !ignoreErrors:
                throw context.ErrorFactory.TooManyParameterValues(descriptor.ParameterName).AsException();
        }

        var stringValue = stringValues[0];

        if (string.Equals(stringValue, OpenIdConstants.DisplayTypes.Page, StringComparison))
        {
            return DisplayType.Page;
        }

        if (string.Equals(stringValue, OpenIdConstants.DisplayTypes.Popup, StringComparison))
        {
            return DisplayType.Popup;
        }

        if (string.Equals(stringValue, OpenIdConstants.DisplayTypes.Touch, StringComparison))
        {
            return DisplayType.Touch;
        }

        if (string.Equals(stringValue, OpenIdConstants.DisplayTypes.Wap, StringComparison))
        {
            return DisplayType.Wap;
        }

        if (ignoreErrors) return null;

        throw context.ErrorFactory.InvalidParameterValue(descriptor.ParameterName).AsException();
    }
}
