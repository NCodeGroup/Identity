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

using System.Text.Json.Nodes;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Represents a single-step migration of a persisted-grant payload's at-rest JSON from one schema version to the next.
/// An upcaster is registered as an enumerable DI service and is applied by <see cref="IPersistedPayloadSerializer"/>
/// when a stored document's schema version is older than the current schema version of the payload type. Chaining one
/// upcaster per version step keeps each migration small, ordered, and independently testable.
/// </summary>
/// <remarks>
/// For every version step between a payload type's oldest-still-stored schema version and its current schema version
/// there must be exactly one registered upcaster; a gap causes <see cref="IPersistedPayloadSerializer.Deserialize{T}"/>
/// to throw rather than silently mis-read data.
/// </remarks>
[PublicAPI]
public interface IPersistedPayloadUpcaster
{
    /// <summary>
    /// Gets the payload type this upcaster migrates. It must match the <c>TPayload</c> type argument used when the
    /// payload is read, which is how the serializer selects the applicable upcasters.
    /// </summary>
    Type PayloadType { get; }

    /// <summary>
    /// Gets the schema version this upcaster migrates <em>from</em>. The upcaster transforms a document at
    /// <see cref="FromVersion"/> into the shape of schema version <c>FromVersion + 1</c>.
    /// </summary>
    int FromVersion { get; }

    /// <summary>
    /// Transforms the payload's JSON from schema version <see cref="FromVersion"/> into the shape expected by schema
    /// version <c>FromVersion + 1</c>.
    /// </summary>
    /// <param name="payload">The payload JSON at schema version <see cref="FromVersion"/>, or <c>null</c> when the
    /// stored payload was the JSON <c>null</c> literal.</param>
    /// <returns>The payload JSON migrated to schema version <c>FromVersion + 1</c>.</returns>
    JsonNode? Upcast(JsonNode? payload);
}
