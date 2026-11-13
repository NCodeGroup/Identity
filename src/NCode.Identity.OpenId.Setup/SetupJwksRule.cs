// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// The rule a JWK Set that setup registers by value satisfies: a JSON object with a non-empty <c>keys</c> array.
/// </summary>
internal static class SetupJwksRule
{
    /// <summary>
    /// Determines whether the <paramref name="value"/> is the JSON text of a JWK Set, a JSON object with a non-empty
    /// <c>keys</c> array.
    /// </summary>
    /// <param name="value">The JSON text to check.</param>
    /// <returns><c>true</c> when the <paramref name="value"/> is a JWK Set; otherwise, <c>false</c>.</returns>
    public static bool IsKeySet(string value)
    {
        try
        {
            var root = JsonElement.Parse(value);
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("keys", out var keys)
                && keys.ValueKind == JsonValueKind.Array
                && keys.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
