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

using System.Text.Json.Serialization;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;

namespace NIdentity.OpenId.Results;

internal class OpenIdError : OpenIdMessage, IOpenIdError
{
    public OpenIdError(string code)
    {
        Code = code;
    }

    // TODO: are these json attributes necessary?

    [JsonIgnore]
    public int? StatusCode { get; set; }

    [JsonIgnore]
    public Exception? Exception { get; set; }

    [JsonPropertyName(OpenIdConstants.Parameters.Code)]
    public string Code
    {
        get => GetKnownParameter(KnownParameters.Code) ?? OpenIdConstants.ErrorCodes.ServerError;
        set => SetKnownParameter(KnownParameters.Code, value);
    }

    [JsonPropertyName(OpenIdConstants.Parameters.ErrorDescription)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description
    {
        get => GetKnownParameter(KnownParameters.ErrorDescription);
        set => SetKnownParameter(KnownParameters.ErrorDescription, value);
    }

    [JsonPropertyName(OpenIdConstants.Parameters.ErrorUri)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Uri
    {
        get => GetKnownParameter(KnownParameters.ErrorUri);
        set => SetKnownParameter(KnownParameters.ErrorUri, value);
    }

    [JsonPropertyName(OpenIdConstants.Parameters.State)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State
    {
        get => GetKnownParameter(KnownParameters.State);
        set => SetKnownParameter(KnownParameters.State, value);
    }
}
