// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
