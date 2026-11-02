// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Contains the parameters for a successful <c>OAuth</c> or <c>OpenID Connect</c> authorization response.
/// </summary>
[PublicAPI]
public interface IAuthorizationTicket : IOpenIdMessageResponse
{
    /// <summary>
    /// Gets or sets when this ticket was created.
    /// </summary>
    DateTimeOffset CreatedWhen { get; set; }

    /// <summary>
    /// Gets or sets the <c>state</c> parameter.
    /// </summary>
    string? State { get; set; }

    /// <summary>
    /// Gets or sets the <c>session_state</c> parameter (OpenID Connect Session Management 1.0), present only when
    /// session management is enabled for the tenant.
    /// </summary>
    string? SessionState { get; set; }

    /// <summary>
    /// Gets or sets the <c>code</c> parameter.
    /// </summary>
    string? AuthorizationCode { get; set; }

    /// <summary>
    /// Gets or sets the <c>id_token</c> parameter.
    /// </summary>
    string? IdToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>access_token</c> parameter.
    /// </summary>
    string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>token_type</c> parameter.
    /// </summary>
    string? TokenType { get; set; }

    /// <summary>
    /// Gets or sets the <c>expires_in</c> parameter.
    /// </summary>
    TimeSpan? ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the <c>iss</c> parameter.
    /// </summary>
    string? Issuer { get; set; }
}
