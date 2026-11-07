// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides the ability to serialize and deserialize settings from JSON.
/// </summary>
/// <remarks>
/// Setting names are a closed vocabulary: every setting in a document must have a registered
/// <see cref="SettingDescriptor"/>, the setting must apply within the registered scope whose document it is
/// (<see cref="ISettingScopeCollection.AppliesWithin(SettingDescriptor, string)"/>), its value must conform to that
/// descriptor's <see cref="SettingDescriptor.ValueType"/>, and the value must be in the setting's domain
/// (<see cref="SettingDescriptor.TryValidate"/>). A document that names an unregistered setting, carries a setting at a
/// scope that never honors it, carries a value of the wrong type, or carries a value outside the setting's domain is
/// rejected rather than loaded with the offending setting silently ignored or silently not honored.
/// </remarks>
[PublicAPI]
public interface ISettingSerializer
{
    /// <summary>
    /// Deserializes a scope's collection of <see cref="Setting"/> instances from JSON. A <c>null</c> or undefined
    /// document yields no settings, and a <c>null</c> setting value yields the setting's registered default.
    /// </summary>
    /// <param name="settingsJson">The JSON to deserialize into <see cref="Setting"/> instances.</param>
    /// <param name="scopeName">The name of the registered scope whose settings document <paramref name="settingsJson"/> is.</param>
    /// <param name="jsonOptions">The <see cref="JsonSerializerOptions"/> to use for deserialization.</param>
    /// <returns>The collection of <see cref="Setting"/> instances.</returns>
    /// <exception cref="JsonException">The document is not a JSON object, or it contains a setting that
    /// <see cref="ValidateSettings"/> reports. The message names every offending setting.</exception>
    /// <exception cref="ArgumentException"><paramref name="scopeName"/> is not a registered scope.</exception>
    IReadOnlyCollection<Setting> DeserializeSettings(
        JsonElement settingsJson,
        string scopeName,
        JsonSerializerOptions jsonOptions
    );

    /// <summary>
    /// Validates a scope's settings document against the registered setting descriptors, reporting every setting that
    /// <see cref="DeserializeSettings"/> would reject: a name with no registered descriptor, a setting that does not
    /// apply within the scope, a value that does not conform to the descriptor's type, a <c>null</c>
    /// value for a setting with no default, or a value outside the setting's domain.
    /// </summary>
    /// <param name="settingsJson">The JSON settings document to validate.</param>
    /// <param name="scopeName">The name of the registered scope whose settings document <paramref name="settingsJson"/> is.</param>
    /// <param name="jsonOptions">The <see cref="JsonSerializerOptions"/> to use for deserialization.</param>
    /// <returns>The <see cref="SettingError"/> instances describing each problem, or an empty list when the document
    /// is valid.</returns>
    /// <exception cref="ArgumentException"><paramref name="scopeName"/> is not a registered scope.</exception>
    IReadOnlyList<SettingError> ValidateSettings(
        JsonElement settingsJson,
        string scopeName,
        JsonSerializerOptions jsonOptions
    );

    /// <summary>
    /// Names the limits (<see cref="SettingDescriptor.IsLimit"/>) whose values differ between a scope's settings
    /// document and the document that replaces it: a limit one document carries and the other does not, or carries
    /// with a different JSON value. A <c>null</c>, undefined or non-object document carries no settings, and a name
    /// with no registered descriptor is no limit.
    /// </summary>
    /// <param name="currentJson">The JSON settings document in force.</param>
    /// <param name="newJson">The JSON settings document that replaces <paramref name="currentJson"/>.</param>
    /// <returns>The names of the changed limits in ordinal order, or an empty list.</returns>
    // See ADR-1418.
    IReadOnlyList<string> GetChangedLimits(JsonElement currentJson, JsonElement newJson);

    /// <summary>
    /// Names the limits (<see cref="SettingDescriptor.IsLimit"/>) a settings document at the registered scope may
    /// carry: each registered limit that applies within the scope.
    /// </summary>
    /// <param name="scopeName">The name of the registered scope whose settings document is described.</param>
    /// <returns>The names in ordinal order, or an empty list.</returns>
    /// <exception cref="ArgumentException"><paramref name="scopeName"/> is not a registered scope.</exception>
    // See ADR-1418.
    IReadOnlyList<string> GetLimits(string scopeName);
}
