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
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse <see cref="CodeChallengeMethod"/> values.
/// </summary>
public class CodeChallengeMethodParser : ParameterParser<CodeChallengeMethod?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(
        OpenIdServer openIdServer,
        CodeChallengeMethod? value)
    {
        return value switch
        {
            CodeChallengeMethod.Plain => OpenIdConstants.CodeChallengeMethods.Plain,
            CodeChallengeMethod.Sha256 => OpenIdConstants.CodeChallengeMethods.S256,
            _ => null
        };
    }

    /// <inheritdoc/>
    public override CodeChallengeMethod? Parse(
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

        if (string.Equals(stringValue, OpenIdConstants.CodeChallengeMethods.Plain, StringComparison))
            return CodeChallengeMethod.Plain;

        if (string.Equals(stringValue, OpenIdConstants.CodeChallengeMethods.S256, StringComparison))
            return CodeChallengeMethod.Sha256;

        if (descriptor.IgnoreUnrecognizedValues)
            return null;

        throw openIdServer.ErrorFactory.InvalidParameterValue(descriptor.ParameterName).AsException();
    }
}
