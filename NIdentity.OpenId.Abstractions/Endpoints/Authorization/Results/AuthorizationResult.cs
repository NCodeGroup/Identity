#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Authorization.Results;

/// <summary>
/// An implementation of <see cref="IOpenIdResult"/> that when executed, issues the response for an
/// <c>OAuth</c> or <c>OpenID Connect</c> authorization operation.
/// </summary>
public class AuthorizationResult : OpenIdResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationResult"/> class.
    /// </summary>
    /// <param name="redirectUri">The <see cref="Uri"/> where the authorization response should be sent to.</param>
    /// <param name="responseMode">The mechanism that should be used for sending the authorization response.</param>
    /// <param name="error">An <see cref="IOpenIdError"/> that contains information about the failure of the operation.</param>
    public AuthorizationResult(Uri redirectUri, ResponseMode responseMode, IOpenIdError error)
        : this(redirectUri, responseMode, error, null)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationResult"/> class.
    /// </summary>
    /// <param name="redirectUri">The <see cref="Uri"/> where the authorization response should be sent to.</param>
    /// <param name="responseMode">The mechanism that should be used for sending the authorization response.</param>
    /// <param name="ticket">An <see cref="IAuthorizationTicket"/> that contains the parameters for a successful
    /// <c>OAuth</c> or <c>OpenID Connect</c> authorization response.</param>
    public AuthorizationResult(Uri redirectUri, ResponseMode responseMode, IAuthorizationTicket ticket)
        : this(redirectUri, responseMode, null, ticket)
    {
        // nothing
    }

    private AuthorizationResult(Uri redirectUri, ResponseMode responseMode, IOpenIdError? error, IAuthorizationTicket? ticket)
    {
        RedirectUri = redirectUri;
        ResponseMode = responseMode;
        Error = error;
        Ticket = ticket;
    }

    /// <summary>
    /// Gets or sets the <see cref="Uri"/> where the authorization response should be sent to.
    /// </summary>
    public Uri RedirectUri { get; }

    /// <summary>
    /// Gets or sets the mechanism that should be used for sending the authorization response.
    /// </summary>
    public ResponseMode ResponseMode { get; }

    /// <summary>
    /// Gets or sets an <see cref="IOpenIdError"/> that contains information about the failure of the operation.
    /// </summary>
    public IOpenIdError? Error { get; }

    /// <summary>
    /// Gets or sets an <see cref="IAuthorizationTicket"/> that contains the parameters for a successful
    /// <c>OAuth</c> or <c>OpenID Connect</c> authorization response.
    /// </summary>
    public IAuthorizationTicket? Ticket { get; }

    /// <inheritdoc />
    public override async ValueTask ExecuteResultAsync(OpenIdEndpointContext context, CancellationToken cancellationToken) =>
        await GetExecutor<AuthorizationResult>(context).ExecuteResultAsync(context, this, cancellationToken);
}
