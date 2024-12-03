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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="IAuthorizationRequest"/>
/// instances to and from JSON.
/// </summary>
public class AuthorizationRequestJsonConverter : JsonConverter<IAuthorizationRequest>
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(IAuthorizationRequest).IsAssignableFrom(typeToConvert);
    }

    /// <inheritdoc />
    public override IAuthorizationRequest? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var envelope = JsonSerializer.Deserialize<AuthorizationRequestJsonEnvelope>(ref reader, options);

        if (envelope.OriginalRequestMessage is null)
            return null;

        var requestMessage = envelope.OriginalRequestMessage;
        Debug.Assert(requestMessage.OpenIdEnvironment != null);

        var requestObject = envelope.OriginalRequestObject;
        Debug.Assert(requestObject == null || requestObject.OpenIdEnvironment == requestMessage.OpenIdEnvironment);

        return new AuthorizationRequest(envelope.IsContinuation, requestMessage, requestObject);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IAuthorizationRequest value, JsonSerializerOptions options)
    {
        var envelope = new AuthorizationRequestJsonEnvelope
        {
            IsContinuation = value.IsContinuation,
            OriginalRequestMessage = value.OriginalRequestMessage,
            OriginalRequestObject = value.OriginalRequestObject
        };
        JsonSerializer.Serialize(writer, envelope, options);
    }
}
