// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Net.Http.Headers;

namespace NCode.Identity.AspNetCore;

// See ADR-1503.
/// <summary>
/// Contains the HTTP logging fields that the guard permits for every request and the headers whose values carry
/// credentials, which the guard never allow-lists.
/// </summary>
internal static class GuardedHttpLoggingDefaults
{
    /// <summary>
    /// The fields that HTTP logging writes for every request: the request and response properties, the allow-listed
    /// headers and the duration. A body is written only for an endpoint that opts in to it.
    /// </summary>
    public const HttpLoggingFields PermittedFields =
        HttpLoggingFields.RequestPropertiesAndHeaders
        | HttpLoggingFields.ResponsePropertiesAndHeaders
        | HttpLoggingFields.Duration;

    /// <summary>
    /// The name of the <c>DPoP</c> request header, whose value is a proof JWT.
    /// </summary>
    public const string DPoPHeaderName = "DPoP";

    /// <summary>
    /// Gets the request headers whose values carry a credential.
    /// </summary>
    public static string[] CredentialRequestHeaders { get; } =
    [HeaderNames.Authorization, HeaderNames.ProxyAuthorization, HeaderNames.Cookie, DPoPHeaderName];

    /// <summary>
    /// Gets the response headers whose values carry a credential.
    /// </summary>
    public static string[] CredentialResponseHeaders { get; } =
    [HeaderNames.SetCookie, HeaderNames.Location];
}
