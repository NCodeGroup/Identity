// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Contains constants that specify from where an authorization request message was loaded from.
/// </summary>
[PublicAPI]
public static class AuthorizationSourceTypes
{
    /// <summary>
    /// Specifies that the message was loaded from the HTTP query data.
    /// </summary>
    public const string Query = RequestValuesSourceTypes.Query;

    /// <summary>
    /// Specifies that the message was loaded from the HTTP form data.
    /// </summary>
    public const string Form = RequestValuesSourceTypes.Form;

    /// <summary>
    /// Specifies that the message was loaded from a JWT-Secured Authorization Request (RFC 9101) object.
    /// </summary>
    public const string Jar = "jar";

    /// <summary>
    /// Specifies that the authorization request was loaded after combining values from the request message (query or form)
    /// and the request (JAR) object.
    /// </summary>
    public const string Union = "union";
}
