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
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse <see cref="GrantType"/> values.
/// </summary>
public class GrantTypeParser : ParameterParser<GrantType?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(
        OpenIdServer openIdServer,
        GrantType? value)
    {
        return value switch
        {
            GrantType.AuthorizationCode => OpenIdConstants.GrantTypes.AuthorizationCode,
            GrantType.Implicit => OpenIdConstants.GrantTypes.Implicit,
            GrantType.Hybrid => OpenIdConstants.GrantTypes.Hybrid,
            GrantType.Password => OpenIdConstants.GrantTypes.Password,
            GrantType.ClientCredentials => OpenIdConstants.GrantTypes.ClientCredentials,
            GrantType.RefreshToken => OpenIdConstants.GrantTypes.RefreshToken,
            GrantType.DeviceCode => OpenIdConstants.GrantTypes.DeviceCode,
            _ => null
        };
    }

    /// <inheritdoc/>
    public override GrantType? Parse(
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        StringValues stringValues)
    {
        Debug.Assert(!descriptor.AllowMultipleValues);

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

        if (string.Equals(stringValue, OpenIdConstants.GrantTypes.AuthorizationCode, StringComparison))
            return GrantType.AuthorizationCode;

        if (string.Equals(stringValue, OpenIdConstants.GrantTypes.Implicit, StringComparison))
            return GrantType.Implicit;

        if (string.Equals(stringValue, OpenIdConstants.GrantTypes.Hybrid, StringComparison))
            return GrantType.Hybrid;

        if (string.Equals(stringValue, OpenIdConstants.GrantTypes.Password, StringComparison))
            return GrantType.Password;

        if (string.Equals(stringValue, OpenIdConstants.GrantTypes.ClientCredentials, StringComparison))
            return GrantType.ClientCredentials;

        if (string.Equals(stringValue, OpenIdConstants.GrantTypes.RefreshToken, StringComparison))
            return GrantType.RefreshToken;

        if (string.Equals(stringValue, OpenIdConstants.GrantTypes.DeviceCode, StringComparison))
            return GrantType.DeviceCode;

        if (descriptor.IgnoreUnrecognizedValues)
            return null;

        throw openIdServer.ErrorFactory.InvalidParameterValue(descriptor.ParameterName).AsException();
    }
}
