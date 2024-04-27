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

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides the ability for an <see cref="IOpenIdMessage"/> to store additional properties that are included in JSON
/// serialization but not persisted as <c>OAuth</c> or <c>OpenID Connect</c> parameters.
/// </summary>
public interface ISupportProperties : IOpenIdMessage
{
    /// <summary>
    /// Writes the additional properties to the specified <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> to control serialization behavior during writing.</param>
    void SerializeProperties(Utf8JsonWriter writer, JsonSerializerOptions options);

    /// <summary>
    /// Reads the additional properties from the specified <paramref name="reader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> to control serialization behavior during reading.</param>
    void DeserializeProperties(ref Utf8JsonReader reader, JsonSerializerOptions options);
}
