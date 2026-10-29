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

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Lets an <see cref="IAuditEvent"/> write its own fields into the <c>details</c> object of the audit record,
/// which is open so each event can carry the fields specific to the operation it audits.
/// </summary>
/// <remarks>
/// An event writes properties only and never wraps them in <see cref="Utf8JsonWriter.WriteStartObject()"/> and
/// <see cref="Utf8JsonWriter.WriteEndObject"/>, because the record's writer owns the surrounding object. Property
/// names are snake_case, like the rest of the record. An event never writes secret material: no raw tokens, client
/// secrets, key material, or authorization codes.
/// </remarks>
[PublicAPI]
public interface ISupportAuditDetails
{
    /// <summary>
    /// Gets a value indicating whether this event has fields to write. When <see langword="false"/>, the record has no
    /// <c>details</c> object at all, rather than an empty one.
    /// </summary>
    bool HasAuditDetails => true;

    /// <summary>
    /// Writes this event's fields as properties of the record's <c>details</c> object.
    /// </summary>
    /// <param name="writer">The writer positioned inside the <c>details</c> object.</param>
    void WriteAuditDetails(Utf8JsonWriter writer);
}
