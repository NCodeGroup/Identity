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
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse <see cref="ResponseMode"/> values.
/// </summary>
public class ResponseModeParser : ParameterParser<ResponseMode?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(
        OpenIdServer openIdServer,
        ResponseMode? value)
    {
        return value switch
        {
            ResponseMode.Query => OpenIdConstants.ResponseModes.Query,
            ResponseMode.Fragment => OpenIdConstants.ResponseModes.Fragment,
            ResponseMode.FormPost => OpenIdConstants.ResponseModes.FormPost,
            _ => null
        };
    }

    /// <inheritdoc/>
    public override ResponseMode? Parse(
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        StringValues stringValues)
    {
        Debug.Assert(!descriptor.AllowMultipleStringValues);

        switch (stringValues.Count)
        {
            case 0 when descriptor.Optional:
                return null;

            case 0:
                throw openIdServer.ErrorFactory.MissingParameter(descriptor.ParameterName).AsException();

            case > 1:
                throw openIdServer.ErrorFactory.TooManyParameterValues(descriptor.ParameterName).AsException();
        }

        var stringValue = stringValues[0];

        if (string.Equals(stringValue, OpenIdConstants.ResponseModes.Query, StringComparison))
            return ResponseMode.Query;

        if (string.Equals(stringValue, OpenIdConstants.ResponseModes.Fragment, StringComparison))
            return ResponseMode.Fragment;

        if (string.Equals(stringValue, OpenIdConstants.ResponseModes.FormPost, StringComparison))
            return ResponseMode.FormPost;

        if (descriptor.IgnoreUnrecognizedValues)
            return null;

        throw openIdServer.ErrorFactory.InvalidParameterValue(descriptor.ParameterName).AsException();
    }
}
