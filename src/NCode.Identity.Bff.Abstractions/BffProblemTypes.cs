// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The known <c>type</c> values of the <c>application/problem+json</c> responses of the BFF.
/// </summary>
/// <remarks>
/// The types are an open vocabulary: a response may carry a type that is not listed here. Types are compared
/// ordinally.
/// </remarks>
[PublicAPI]
public static class BffProblemTypes
{
    /// <summary>
    /// The request has no valid session.
    /// </summary>
    public const string SessionRequired = "urn:ncode:identity:bff:session_required";

    /// <summary>
    /// The request lacks the anti-forgery header, or the header is wrong.
    /// </summary>
    public const string AntiforgeryHeaderMissing =
        "urn:ncode:identity:bff:antiforgery_header_missing";

    /// <summary>
    /// The request comes from another site that is not trusted.
    /// </summary>
    public const string CrossSiteRequest = "urn:ncode:identity:bff:cross_site_request";

    /// <summary>
    /// The session is not allowed to perform the request.
    /// </summary>
    public const string Forbidden = "urn:ncode:identity:bff:forbidden";

    /// <summary>
    /// The session store cannot be reached.
    /// </summary>
    public const string StoreUnavailable = "urn:ncode:identity:bff:store_unavailable";

    /// <summary>
    /// The access token that the request needs is not available.
    /// </summary>
    public const string TokenUnavailable = "urn:ncode:identity:bff:token_unavailable";

    /// <summary>
    /// The request is malformed.
    /// </summary>
    public const string InvalidRequest = "urn:ncode:identity:bff:invalid_request";

    /// <summary>
    /// A form post to a page lacks a valid antiforgery token.
    /// </summary>
    public const string AntiforgeryTokenInvalid =
        "urn:ncode:identity:bff:antiforgery_token_invalid";

    /// <summary>
    /// The request to a remote API uses a method that the remote API does not accept.
    /// </summary>
    public const string MethodNotAllowed = "urn:ncode:identity:bff:method_not_allowed";
}
