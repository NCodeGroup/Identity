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
using System.Text;
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

        var errorCode = ReadString(json, OpenIdProtocol.Parameters.ErrorCode);
        if (string.IsNullOrEmpty(errorCode))
            return false;

        error = new ProtocolError(errorCode, response.StatusCode)
        {
            ErrorDescription = ReadString(json, OpenIdProtocol.Parameters.ErrorDescription),
            ErrorUri = ReadString(json, OpenIdProtocol.Parameters.ErrorUri),
        };
        return true;
    }

    /// <summary>
    /// Reads a <see cref="ProtocolError"/> from the <c>error</c>, <c>error_description</c> and <c>error_uri</c>
    /// auth-params of the response's <c>WWW-Authenticate</c> challenge, with which a protected resource reports an error
    /// (RFC 6750 §3, RFC 9449 §7.1).
    /// </summary>
    public static bool TryReadChallenge(
        ProviderHttpResponse response,
        [NotNullWhen(true)] out ProtocolError? error
    )
    {
        error = null;

        if (string.IsNullOrEmpty(response.WwwAuthenticate))
            return false;

        var errorCode = ReadAuthParam(
            response.WwwAuthenticate,
            OpenIdProtocol.Parameters.ErrorCode
        );
        if (string.IsNullOrEmpty(errorCode))
            return false;

        error = new ProtocolError(errorCode, response.StatusCode)
        {
            ErrorDescription = ReadAuthParam(
                response.WwwAuthenticate,
                OpenIdProtocol.Parameters.ErrorDescription
            ),
            ErrorUri = ReadAuthParam(response.WwwAuthenticate, OpenIdProtocol.Parameters.ErrorUri),
        };
        return true;
    }

    // An auth-param is name=token or name=quoted-string (RFC 9110 §11.2); a name matches only at the start of the
    // challenge's parameters or after a separator, so error does not match inside error_description.
    private static string? ReadAuthParam(string challenge, string name)
    {
        var position = 0;
        while (position < challenge.Length)
        {
            var index = challenge.IndexOf(name, position, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return null;

            position = index + name.Length;
            var atBoundary = index == 0 || challenge[index - 1] is ' ' or ',' or '\t';
            var afterName = position;
            while (afterName < challenge.Length && challenge[afterName] is ' ' or '\t')
                afterName++;

            if (!atBoundary || afterName >= challenge.Length || challenge[afterName] != '=')
                continue;

            var valueStart = afterName + 1;
            while (valueStart < challenge.Length && challenge[valueStart] is ' ' or '\t')
                valueStart++;

            if (valueStart >= challenge.Length)
                return string.Empty;

            if (challenge[valueStart] != '"')
            {
                var tokenEnd = challenge.IndexOfAny([',', ' ', '\t'], valueStart);
                return tokenEnd < 0 ? challenge[valueStart..] : challenge[valueStart..tokenEnd];
            }

            var builder = new StringBuilder();
            for (var i = valueStart + 1; i < challenge.Length; i++)
            {
                var c = challenge[i];
                if (c == '\\' && i + 1 < challenge.Length)
                {
                    builder.Append(challenge[++i]);
                    continue;
                }

                if (c == '"')
                    return builder.ToString();

                builder.Append(c);
            }

            return builder.ToString();
        }

        return null;
    }

    private static string? ReadString(JsonElement json, string name) =>
        json.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
