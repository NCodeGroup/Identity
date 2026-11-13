// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Provides the helpers that client creation and client settings evaluation share for a client's settings document.
/// </summary>
internal static class ClientSettingsJson
{
    /// <summary>
    /// Converts a client settings document into a mutable <see cref="JsonObject"/>, returning an empty object when the
    /// element is <c>null</c> or not a JSON object.
    /// </summary>
    /// <param name="element">The settings document to convert.</param>
    /// <returns>The resulting <see cref="JsonObject"/>.</returns>
    internal static JsonObject ToJsonObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Object:
                return JsonObject.Create(element) ?? [];

            default:
                return [];
        }
    }

    /// <summary>
    /// Groups setting errors by setting name, in the order the settings first appear, as a validation problem lists
    /// them.
    /// </summary>
    /// <param name="errors">The <see cref="SettingError"/> instances to group.</param>
    /// <returns>The messages of each setting name.</returns>
    internal static Dictionary<string, string[]> ToSettingErrors(
        IReadOnlyList<SettingError> errors
    ) =>
        errors
            .GroupBy(error => error.SettingName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray(),
                StringComparer.Ordinal
            );
}
