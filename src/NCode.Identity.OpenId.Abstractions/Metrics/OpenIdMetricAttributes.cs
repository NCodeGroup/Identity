// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace NCode.Identity.OpenId.Metrics;

/// <summary>
/// Names the bounded attributes the OpenID protocol endpoints add to ASP.NET Core's
/// <c>http.server.request.duration</c>, and adds them through <see cref="IHttpMetricsTagsFeature"/> so every component
/// that writes a protocol response tags it the same way.
/// </summary>
// See ADR-1197.
public static class OpenIdMetricAttributes
{
    /// <summary>
    /// The registered name of the protocol endpoint that served the request, such as <c>token_endpoint</c>.
    /// </summary>
    public const string Endpoint = "ncode.identity.openid.endpoint";

    /// <summary>
    /// The OAuth error code of an error response, such as <c>invalid_grant</c>; always chosen by the server.
    /// </summary>
    public const string Error = "ncode.identity.openid.error";

    /// <summary>
    /// The grant type of a token request when it is a registered one, else <see cref="OtherValue"/>.
    /// </summary>
    public const string GrantType = "ncode.identity.openid.grant_type";

    /// <summary>
    /// The value an attribute takes when the request names something the server does not recognize, following
    /// OpenTelemetry's convention, so a caller cannot create a series per value it invents.
    /// </summary>
    public const string OtherValue = "_OTHER";

    /// <summary>
    /// Adds an attribute to the request's <c>http.server.request.duration</c> measurement, if the server records it.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
    /// <param name="name">The attribute name, one of the constants of this class.</param>
    /// <param name="value">The bounded attribute value.</param>
    public static void Add(HttpContext httpContext, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext
            .Features.Get<IHttpMetricsTagsFeature>()
            ?.Tags.Add(new KeyValuePair<string, object?>(name, value));
    }
}
