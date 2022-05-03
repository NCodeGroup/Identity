#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse <see cref="DisplayType"/> values.
/// </summary>
public class DisplayTypeParser : ParameterParser<DisplayType?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(IOpenIdMessageContext context, DisplayType? value)
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
    public override DisplayType? Parse(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues)
    {
        Debug.Assert(!descriptor.AllowMultipleValues);

        switch (stringValues.Count)
        {
            case 0 when descriptor.Optional:
                return null;

            case 0:
                throw OpenIdException.Factory.MissingParameter(descriptor.ParameterName);

            case > 1:
                throw OpenIdException.Factory.TooManyParameterValues(descriptor.ParameterName);
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

        throw OpenIdException.Factory.InvalidRequest(descriptor.ParameterName);
    }
}
