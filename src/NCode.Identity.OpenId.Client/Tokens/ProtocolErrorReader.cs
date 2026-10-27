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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NCode.Identity.OpenId.Client.Http;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Reads the OAuth error members of a provider response.
/// </summary>
internal static class ProtocolErrorReader
{
    /// <summary>
    /// Reads a <see cref="ProtocolError"/> when the response body is an error object.
    /// </summary>
    public static bool TryRead(
        ProviderHttpResponse response,
        [NotNullWhen(true)] out ProtocolError? error
    )
    {
        error = null;

        if (response.Json is not { ValueKind: JsonValueKind.Object } json)
            return false;

        var errorCode = ReadString(json, "error");
        if (string.IsNullOrEmpty(errorCode))
            return false;

        error = new ProtocolError(errorCode, response.StatusCode)
        {
            ErrorDescription = ReadString(json, "error_description"),
            ErrorUri = ReadString(json, "error_uri"),
        };
        return true;
    }

    private static string? ReadString(JsonElement json, string name) =>
        json.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
