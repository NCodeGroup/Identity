// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Net.Http.Headers;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Hardening;

/// <summary>
/// Creates the security headers that <c>UseBffSecurityHeaders()</c> writes from a frontend's options.
/// </summary>
internal static class BffSecurityHeaders
{
    private const string NoSniff = "nosniff";
    private const string ReferrerPolicyHeaderName = "Referrer-Policy";
    private const string CrossOriginOpenerPolicyHeaderName = "Cross-Origin-Opener-Policy";

    /// <summary>
    /// Gets the strict default headers that a request gets when no frontend selects it.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Default { get; } =
        Create(new BffSecurityHeadersOptions());

    /// <summary>
    /// Creates the security headers from a frontend's options.
    /// </summary>
    /// <param name="options">The security header options of the frontend.</param>
    /// <returns>The name and value of each header to write.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> Create(
        BffSecurityHeadersOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        return
        [
            new(HeaderNames.ContentSecurityPolicy, options.ContentSecurityPolicy),
            new(HeaderNames.XContentTypeOptions, NoSniff),
            new(ReferrerPolicyHeaderName, options.ReferrerPolicy),
            new(CrossOriginOpenerPolicyHeaderName, options.CrossOriginOpenerPolicy),
        ];
    }
}
