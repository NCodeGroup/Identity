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

namespace NCode.Identity.Json;

/// <summary>
/// Provides shared, reusable <see cref="JsonElement"/> values and factories. These avoid the common pitfalls of
/// hand-rolling an "empty object" element: forgetting to <see cref="JsonElement.Clone"/> a value out of a
/// <see cref="JsonDocument"/> before it is disposed (which yields an <see cref="ObjectDisposedException"/> on later
/// access), and accidentally using a <see langword="default"/> element whose <see cref="JsonElement.ValueKind"/> is
/// <see cref="JsonValueKind.Undefined"/> rather than a well-formed object.
/// </summary>
[PublicAPI]
public static class JsonElements
{
    private static readonly JsonElement EmptyObjectValue = JsonDocument
        .Parse("{}")
        .RootElement.Clone();

    /// <summary>
    /// Gets a cached, immutable, detached <see cref="JsonElement"/> representing an empty JSON object (<c>{}</c>). The
    /// value is safe to share and re-use because it is cloned free of its owning <see cref="JsonDocument"/>.
    /// </summary>
    public static JsonElement EmptyObject => EmptyObjectValue;

    extension(JsonElement jsonElement)
    {
        /// <summary>
        /// Gets a value indicating whether this element is absent — its <see cref="JsonElement.ValueKind"/> is
        /// <see cref="JsonValueKind.Null"/> or <see cref="JsonValueKind.Undefined"/> — rather than a present value
        /// (which includes an empty object or array).
        /// </summary>
        public bool IsNullOrUndefined() =>
            jsonElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

        /// <summary>
        /// Returns this element when it is present, or the shared <see cref="EmptyObject"/> when it is null or
        /// undefined. Useful at persistence boundaries where an absent JSON bag should round-trip as an empty object
        /// rather than a malformed <c>Undefined</c> element.
        /// </summary>
        public JsonElement OrEmptyObject() =>
            jsonElement.IsNullOrUndefined() ? EmptyObject : jsonElement;
    }
}
