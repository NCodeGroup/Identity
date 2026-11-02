// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Messages;

/// <summary>
/// Contains constants that specify from where an <see cref="IRequestValues"/> was loaded from.
/// </summary>
[PublicAPI]
public static class RequestValuesSourceTypes
{
    /// <summary>
    /// Specifies that the request was loaded from the HTTP query data.
    /// </summary>
    public const string Query = "query";

    /// <summary>
    /// Specifies that the request was loaded from the HTTP form data.
    /// </summary>
    public const string Form = "form";

    /// <summary>
    /// Specifies that the request was replayed from a pushed authorization request (RFC 9126) that the client
    /// previously stored at the pushed authorization request endpoint.
    /// </summary>
    public const string PushedAuthorizationRequest = "pushed_authorization_request";

    /// <summary>
    /// Specifies that the values were read from a verified <c>request object</c> (RFC 9101).
    /// </summary>
    public const string RequestObject = "request_object";
}
