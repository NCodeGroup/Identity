// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Net.Http.Headers;
using Microsoft.Net.Http.Headers;
using NCode.Identity.Bff.Tokens;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Keeps the BFF's own cookies on the BFF for every request that a YARP route forwards: the session and login cookies
/// never reach an upstream, and an upstream can never set them. A request that carries a resolved BFF token goes to a
/// remote API, so it loses every cookie and its response loses every <c>Set-Cookie</c>.
/// </summary>
internal sealed class BffCookieBoundaryHandler : DelegatingHandler
{
    private static FrozenSet<string> BffCookieNames { get; } =
        FrozenSet.Create(
            StringComparer.Ordinal,
            BffDefaults.SessionCookieName,
            BffDefaults.LoginCookieName
        );

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var isRemoteApi = request.Options.TryGetValue(
            BffAccessTokenHandler.ResolvedTokenKey,
            out _
        );
        if (isRemoteApi)
            request.Headers.Remove(HeaderNames.Cookie);
        else
            RemoveBffCookies(request.Headers);

        var response = await base.SendAsync(request, cancellationToken);

        if (isRemoteApi)
            response.Headers.Remove(HeaderNames.SetCookie);
        else
            RemoveBffSetCookies(response.Headers);

        return response;
    }

    private static void RemoveBffCookies(HttpRequestHeaders headers)
    {
        if (!headers.TryGetValues(HeaderNames.Cookie, out var values))
            return;

        List<string> kept = [];
        var isRemoved = false;
        foreach (var value in values)
        {
            foreach (
                var pair in value.Split(
                    ';',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                if (IsBffCookie(pair))
                    isRemoved = true;
                else
                    kept.Add(pair);
            }
        }

        if (!isRemoved)
            return;

        headers.Remove(HeaderNames.Cookie);
        if (kept.Count > 0)
            headers.TryAddWithoutValidation(HeaderNames.Cookie, string.Join("; ", kept));
    }

    private static void RemoveBffSetCookies(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues(HeaderNames.SetCookie, out var values))
            return;

        var all = values.ToList();
        var kept = all.Where(value => !IsBffCookie(value)).ToList();
        if (kept.Count == all.Count)
            return;

        headers.Remove(HeaderNames.SetCookie);
        foreach (var value in kept)
            headers.TryAddWithoutValidation(HeaderNames.SetCookie, value);
    }

    // The name of a cookie in a Cookie pair or a Set-Cookie value ends at its first '=', or at its first ';'.
    private static bool IsBffCookie(string cookie)
    {
        var end = cookie.AsSpan().IndexOfAny('=', ';');
        var name = end < 0 ? cookie.AsSpan() : cookie.AsSpan(0, end);
        return BffCookieNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(name.Trim());
    }
}
