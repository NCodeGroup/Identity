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

namespace NCode.Identity.OpenId.Client.Http;

/// <summary>
/// Validates the endpoints that the client is willing to call.
/// </summary>
internal static class ProviderUri
{
    /// <summary>
    /// Parses an absolute endpoint that is either <c>https</c>, or <c>http</c> on a loopback host.
    /// </summary>
    public static bool TryParseEndpoint(string? value, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;

        if (
            value is null
            || !(
                value.StartsWith("https://", StringComparison.Ordinal)
                || value.StartsWith("http://", StringComparison.Ordinal)
            )
            || value.Any(char.IsWhiteSpace)
            || value.Contains('#')
        )
            return false;

        if (!AbsoluteUri.TryParse(value, out var parsed))
            return false;

        var isAllowed =
            parsed.Scheme == Uri.UriSchemeHttps
            || (parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback);
        if (!isAllowed)
            return false;

        uri = parsed;
        return true;
    }
}
