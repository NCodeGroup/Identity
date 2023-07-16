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

namespace NIdentity.OpenId.Messages;

/// <summary>
/// Provides the ability for an <see cref="IOpenIdMessage"/> to store additional properties that are included in JSON
/// serialization but not persisted as <c>OAuth</c> or <c>OpenId Connect</c> parameters.
/// </summary>
internal interface ISupportProperties : IOpenIdMessage
{
    void SerializeProperties(Utf8JsonWriter writer, JsonSerializerOptions options);

    void DeserializeProperties(ref Utf8JsonReader reader, JsonSerializerOptions options);
}
