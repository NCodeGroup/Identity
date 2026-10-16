#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Provides the single seam through which persisted-grant payloads are serialized to, and deserialized from, their
/// at-rest JSON form. Every stored document is wrapped in a self-describing, schema-versioned envelope so the schema
/// version always travels with the data and future non-additive changes can be migrated by a registered
/// <see cref="IPersistedPayloadUpcaster"/> without a storage migration. The current schema version of a payload type is
/// declared with <see cref="PersistedPayloadVersionAttribute"/> (defaulting to version <c>1</c> when absent).
/// </summary>
[PublicAPI]
public interface IPersistedPayloadSerializer
{
    /// <summary>
    /// Serializes the specified payload into its at-rest JSON form, wrapping it in a schema-versioned envelope stamped
    /// with the current schema version of <typeparamref name="TPayload"/>.
    /// </summary>
    /// <param name="payload">The payload to serialize.</param>
    /// <param name="jsonOptions">The <see cref="JsonSerializerOptions"/> used to serialize the payload members.</param>
    /// <typeparam name="TPayload">The type of the payload being serialized.</typeparam>
    /// <returns>The <see cref="JsonElement"/> containing the schema-versioned at-rest document.</returns>
    JsonElement Serialize<TPayload>(TPayload payload, JsonSerializerOptions jsonOptions);

    /// <summary>
    /// Deserializes an at-rest JSON document back into its payload, applying any registered
    /// <see cref="IPersistedPayloadUpcaster"/> steps needed to migrate the stored schema version up to the current
    /// schema version of <typeparamref name="TPayload"/>. A document written before this envelope existed (one lacking
    /// the version marker) is read as schema version <c>1</c>.
    /// </summary>
    /// <param name="document">The <see cref="JsonElement"/> containing the at-rest document.</param>
    /// <param name="jsonOptions">The <see cref="JsonSerializerOptions"/> used to deserialize the payload members.</param>
    /// <typeparam name="TPayload">The type of the payload being deserialized.</typeparam>
    /// <returns>The deserialized payload.</returns>
    /// <exception cref="JsonException">The document could not be deserialized, or an upcaster needed to migrate the
    /// stored schema version to the current schema version was not registered.</exception>
    TPayload Deserialize<TPayload>(JsonElement document, JsonSerializerOptions jsonOptions);
}
