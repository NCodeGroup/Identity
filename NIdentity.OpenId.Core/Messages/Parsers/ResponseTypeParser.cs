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
using NIdentity.OpenId.Servers;

namespace NIdentity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse <see cref="ResponseTypes"/> values.
/// </summary>
public class ResponseTypeParser : ParameterParser<ResponseTypes?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(
        OpenIdServer openIdServer,
        ResponseTypes? value)
    {
        if (value is null or ResponseTypes.Unspecified)
            return StringValues.Empty;

        const int capacity = 4;
        var responseType = value.Value;
        var list = new List<string>(capacity);

        if (responseType.HasFlag(ResponseTypes.None))
            list.Add(OpenIdConstants.ResponseTypes.None);

        if (responseType.HasFlag(ResponseTypes.Code))
            list.Add(OpenIdConstants.ResponseTypes.Code);

        if (responseType.HasFlag(ResponseTypes.IdToken))
            list.Add(OpenIdConstants.ResponseTypes.IdToken);

        if (responseType.HasFlag(ResponseTypes.Token))
            list.Add(OpenIdConstants.ResponseTypes.Token);

        return string.Join(Separator, list);
    }

    /// <inheritdoc/>
    public override ResponseTypes? Parse(
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

        stringValues = stringValues[0]!.Split(Separator);

        var responseType = ResponseTypes.Unspecified;
        foreach (var stringValue in stringValues)
        {
            if (string.Equals(stringValue, OpenIdConstants.ResponseTypes.None, StringComparison))
            {
                responseType |= ResponseTypes.None;
            }
            else if (string.Equals(stringValue, OpenIdConstants.ResponseTypes.Code, StringComparison))
            {
                responseType |= ResponseTypes.Code;
            }
            else if (string.Equals(stringValue, OpenIdConstants.ResponseTypes.IdToken, StringComparison))
            {
                responseType |= ResponseTypes.IdToken;
            }
            else if (string.Equals(stringValue, OpenIdConstants.ResponseTypes.Token, StringComparison))
            {
                responseType |= ResponseTypes.Token;
            }
            else if (!descriptor.IgnoreUnrecognizedValues)
            {
                throw openIdServer.ErrorFactory.InvalidParameterValue(descriptor.ParameterName).AsException();
            }
        }

        return responseType;
    }
}
