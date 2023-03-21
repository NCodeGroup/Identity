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

// TODO: unit test

internal class AuthorizationRequestMessageJsonConverter : JsonConverter<IAuthorizationRequestMessage?>
{
    public override IAuthorizationRequestMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<AuthorizationRequestMessage>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, IAuthorizationRequestMessage? value, JsonSerializerOptions options)
    {
        var type = value?.GetType() ?? typeof(AuthorizationRequestMessage);
        JsonSerializer.Serialize(writer, value, type, options);
    }
}
