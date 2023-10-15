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
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse <see cref="PromptTypes"/> values.
/// </summary>
public class PromptTypeParser : ParameterParser<PromptTypes?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(
        OpenIdContext context,
        PromptTypes? value)
    {
        if (value is null or PromptTypes.Unspecified)
            return StringValues.Empty;

        const int capacity = 5;
        var list = new List<string>(capacity);
        var promptType = value.Value;

        if (promptType.HasFlag(PromptTypes.None))
            list.Add(OpenIdConstants.PromptTypes.None);

        if (promptType.HasFlag(PromptTypes.Login))
            list.Add(OpenIdConstants.PromptTypes.Login);

        if (promptType.HasFlag(PromptTypes.Consent))
            list.Add(OpenIdConstants.PromptTypes.Consent);

        if (promptType.HasFlag(PromptTypes.SelectAccount))
            list.Add(OpenIdConstants.PromptTypes.SelectAccount);

        if (promptType.HasFlag(PromptTypes.CreateAccount))
            list.Add(OpenIdConstants.PromptTypes.CreateAccount);

        return string.Join(Separator, list);
    }

    /// <inheritdoc/>
    public override PromptTypes? Parse(
        OpenIdContext context,
        ParameterDescriptor descriptor,
        StringValues stringValues,
        bool ignoreErrors = false)
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

        stringValues = stringValues[0]!.Split(Separator);

        var promptType = PromptTypes.Unspecified;
        foreach (var stringValue in stringValues)
        {
            if (string.Equals(stringValue, OpenIdConstants.PromptTypes.None, StringComparison))
            {
                promptType |= PromptTypes.None;
            }
            else if (string.Equals(stringValue, OpenIdConstants.PromptTypes.Login, StringComparison))
            {
                promptType |= PromptTypes.Login;
            }
            else if (string.Equals(stringValue, OpenIdConstants.PromptTypes.Consent, StringComparison))
            {
                promptType |= PromptTypes.Consent;
            }
            else if (string.Equals(stringValue, OpenIdConstants.PromptTypes.SelectAccount, StringComparison))
            {
                promptType |= PromptTypes.SelectAccount;
            }
            else if (string.Equals(stringValue, OpenIdConstants.PromptTypes.CreateAccount, StringComparison))
            {
                promptType |= PromptTypes.CreateAccount;
            }
            else if (!ignoreErrors)
            {
                // TODO: ignore unsupported values
                throw context.ErrorFactory.InvalidParameterValue(descriptor.ParameterName).AsException();
            }
        }

        return promptType;
    }
}
