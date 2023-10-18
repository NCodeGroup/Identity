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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Endpoints.Authorization.Messages;

internal class AuthorizationRequestJsonConverter : JsonConverter<IAuthorizationRequest>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(IAuthorizationRequest).IsAssignableFrom(typeToConvert);
    }

    public override IAuthorizationRequest? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // FYI, the messages will already be initialized with the context
        var envelope = JsonSerializer.Deserialize<AuthorizationRequestJsonEnvelope>(ref reader, options);

        var requestMessage = envelope?.OriginalRequestMessage;
        if (requestMessage == null)
            return null;

        var requestObject = envelope?.OriginalRequestObject;

        return new AuthorizationRequest(requestMessage, requestObject);
    }

    public override void Write(Utf8JsonWriter writer, IAuthorizationRequest value, JsonSerializerOptions options)
    {
        var envelope = new AuthorizationRequestJsonEnvelope
        {
            OriginalRequestMessage = value.OriginalRequestMessage,
            OriginalRequestObject = value.OriginalRequestObject
        };
        JsonSerializer.Serialize(writer, envelope, options);
    }
}
