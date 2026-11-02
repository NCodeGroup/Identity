// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Specifies from where the JWT-Secured Authorization Request (JAR) object was loaded from.
/// </summary>
[PublicAPI]
public enum RequestObjectSource
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Specifies that the <c>Request Object</c> was loaded from the <c>request</c> parameter.
    /// </summary>
    Inline,

    /// <summary>
    /// Specifies that the <c>Request Object</c> was loaded by the fetching <c>request_uri</c> parameter.
    /// </summary>
    Remote,
}
