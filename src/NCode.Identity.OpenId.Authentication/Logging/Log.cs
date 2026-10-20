#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.OpenId.Authentication.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.OpenId.Authentication</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.AuthorizationRequestNotHandled,
        Level = LogLevel.Error,
        Message = "The authorization request was not handled."
    )]
    internal static partial void AuthorizationRequestNotHandled(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.ClientRequestedAccountCreation,
        Level = LogLevel.Information,
        Message = "Client requested account creation."
    )]
    internal static partial void ClientRequestedAccountCreation(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.ClientRequestedReAuthentication,
        Level = LogLevel.Information,
        Message = "Client requested re-authentication."
    )]
    internal static partial void ClientRequestedReAuthentication(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.ConsentRequired,
        Level = LogLevel.Information,
        Message = "Consent is required for the requested scopes."
    )]
    internal static partial void ConsentRequired(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.FailedToDecodeJwt,
        Level = LogLevel.Warning,
        Message = "Failed to decode JWT"
    )]
    internal static partial void FailedToDecodeJwt(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.FailedToDeserializeJson,
        Level = LogLevel.Warning,
        Message = "Failed to deserialize JSON"
    )]
    internal static partial void FailedToDeserializeJson(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.FailedToFetchRequestUri,
        Level = LogLevel.Warning,
        Message = "Failed to fetch the request URI"
    )]
    internal static partial void FailedToFetchRequestUri(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.EndUserSessionAuthenticationFailed,
        Level = LogLevel.Information,
        Message = "The end-user session could not be authenticated; treating as no session and prompting for login."
    )]
    internal static partial void EndUserSessionAuthenticationFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ClientAuthenticationClientNotFound,
        Level = LogLevel.Information,
        Message = "Client authentication failed: client '{ClientId}' was not found, is disabled, or does not belong to tenant '{TenantId}'."
    )]
    internal static partial void ClientAuthenticationClientNotFound(
        this ILogger logger,
        string clientId,
        string tenantId
    );

    [LoggerMessage(
        EventId = EventIds.ClientAuthenticationCredentialDeserializationFailed,
        Level = LogLevel.Warning,
        Message = "Client authentication failed: a stored credential for client '{ClientId}' could not be deserialized (for example, a secret protected by an unavailable data-protection key)."
    )]
    internal static partial void ClientAuthenticationCredentialDeserializationFailed(
        this ILogger logger,
        string clientId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ClientAuthenticationSecretMismatch,
        Level = LogLevel.Information,
        Message = "Client authentication failed: the presented secret for client '{ClientId}' did not match any stored secret."
    )]
    internal static partial void ClientAuthenticationSecretMismatch(
        this ILogger logger,
        string clientId
    );

    [LoggerMessage(
        EventId = EventIds.ClientAuthenticationMethodNotRegistered,
        Level = LogLevel.Information,
        Message = "Client authentication failed: client '{ClientId}' authenticated with '{AuthenticationMethod}', but it is registered to authenticate with '{RegisteredAuthenticationMethod}'."
    )]
    internal static partial void ClientAuthenticationMethodNotRegistered(
        this ILogger logger,
        string clientId,
        string authenticationMethod,
        string registeredAuthenticationMethod
    );

    [LoggerMessage(
        EventId = EventIds.ClientAuthenticationMethodNotPermitted,
        Level = LogLevel.Warning,
        Message = "Client authentication failed: client '{ClientId}' is registered to authenticate with '{RegisteredAuthenticationMethod}', which its token_endpoint_auth_methods_supported does not permit."
    )]
    internal static partial void ClientAuthenticationMethodNotPermitted(
        this ILogger logger,
        string clientId,
        string registeredAuthenticationMethod
    );

    [LoggerMessage(
        EventId = EventIds.ClientAssertionInvalid,
        Level = LogLevel.Information,
        Message = "Client authentication failed: the client_assertion presented by client '{ClientId}' could not be validated (signature, issuer/subject, audience, lifetime, or claims)."
    )]
    internal static partial void ClientAssertionInvalid(
        this ILogger logger,
        string clientId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ClientAssertionReplayed,
        Level = LogLevel.Information,
        Message = "Client authentication failed: the client_assertion presented by client '{ClientId}' has already been used (replay)."
    )]
    internal static partial void ClientAssertionReplayed(this ILogger logger, string clientId);

    [LoggerMessage(
        EventId = EventIds.TokenRequestClientAuthenticationRequired,
        Level = LogLevel.Warning,
        Message = "The token request was rejected as invalid_client because no client authentication was recognized (every client authentication handler returned undefined; the request carried no credential of the method the client is registered to use)."
    )]
    internal static partial void TokenRequestClientAuthenticationRequired(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.MissingStateParameter,
        Level = LogLevel.Information,
        Message = "Missing 'state' parameter."
    )]
    internal static partial void MissingStateParameter(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.InvalidStateParameter,
        Level = LogLevel.Information,
        Message = "Invalid 'state' parameter."
    )]
    internal static partial void InvalidStateParameter(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.ContinueProviderNotHandled,
        Level = LogLevel.Error,
        Message = "The continue provider did not handle the request."
    )]
    internal static partial void ContinueProviderNotHandled(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.PasswordGrantNotSupported,
        Level = LogLevel.Warning,
        Message = "The resource owner password credential grant type is not supported. "
            + "Please register an implementation of "
            + "`ICommandResponseHandler<AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition>` "
            + "that can handle the resource owner password credential grant type."
    )]
    internal static partial void PasswordGrantNotSupported(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.SubjectValidationFailed,
        Level = LogLevel.Warning,
        Message = "Subject validation failed: {Reason}"
    )]
    internal static partial void SubjectValidationFailed(this ILogger logger, string reason);

    [LoggerMessage(
        EventId = EventIds.OpenIdErrorAuditFailed,
        Level = LogLevel.Warning,
        Message = "Failed to publish the OpenID error audit event."
    )]
    internal static partial void OpenIdErrorAuditFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.UnexpectedOpenIdError,
        Level = LogLevel.Error,
        Message = "An unexpected exception was rendered as an OpenID '{ErrorCode}' error response."
    )]
    internal static partial void UnexpectedOpenIdError(
        this ILogger logger,
        string errorCode,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.OpenIdErrorException,
        Level = LogLevel.Debug,
        Message = "An OpenID '{ErrorCode}' error response carries a triggering exception."
    )]
    internal static partial void OpenIdErrorException(
        this ILogger logger,
        string errorCode,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.AccessTokenInvalid,
        Level = LogLevel.Debug,
        Message = "An access token presented to a protected endpoint failed validation (protection, type, issuer, audience, lifetime, or session) and was rejected as invalid_token."
    )]
    internal static partial void AccessTokenInvalid(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.EndSessionIdTokenHintInvalid,
        Level = LogLevel.Warning,
        Message = "RP-initiated logout: the supplied id_token_hint could not be validated and was ignored."
    )]
    internal static partial void EndSessionIdTokenHintInvalid(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.EndSessionSubjectUnresolved,
        Level = LogLevel.Information,
        Message = "RP-initiated logout: no subject or session could be resolved; only the OP login cookie was cleared."
    )]
    internal static partial void EndSessionSubjectUnresolved(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.EndSessionPostLogoutRedirectUriRejected,
        Level = LogLevel.Warning,
        Message = "RP-initiated logout: post_logout_redirect_uri '{PostLogoutRedirectUri}' is not registered for client '{ClientId}'; not redirecting."
    )]
    internal static partial void EndSessionPostLogoutRedirectUriRejected(
        this ILogger logger,
        string postLogoutRedirectUri,
        string clientId
    );

    [LoggerMessage(
        EventId = EventIds.EndSessionCompleted,
        Level = LogLevel.Information,
        Message = "RP-initiated logout completed; notified {ClientCount} participating client(s) via back-channel logout."
    )]
    internal static partial void EndSessionCompleted(this ILogger logger, int clientCount);

    [LoggerMessage(
        EventId = EventIds.SessionCreated,
        Level = LogLevel.Information,
        Message = "Established end-user session '{SessionId}' for subject '{SubjectId}'."
    )]
    internal static partial void SessionCreated(
        this ILogger logger,
        string sessionId,
        string subjectId
    );

    [LoggerMessage(
        EventId = EventIds.SessionIdleExpired,
        Level = LogLevel.Information,
        Message = "End-user session '{SessionId}' expired after exceeding its idle timeout."
    )]
    internal static partial void SessionIdleExpired(this ILogger logger, string sessionId);

    [LoggerMessage(
        EventId = EventIds.SessionEvicted,
        Level = LogLevel.Information,
        Message = "Evicted end-user session '{SessionId}' to enforce the subject's concurrent-session limit."
    )]
    internal static partial void SessionEvicted(this ILogger logger, string sessionId);

    [LoggerMessage(
        EventId = EventIds.EndUserSessionExpired,
        Level = LogLevel.Information,
        Message = "The end-user session is no longer valid (expired or terminated); treating as no session and prompting for login."
    )]
    internal static partial void EndUserSessionExpired(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.ClientJwksFetchFailed,
        Level = LogLevel.Warning,
        Message = "The client 'jwks_uri' ('{JwksUri}') for client '{ClientId}' could not be fetched or its key set was refused; the client's published keys are unavailable."
    )]
    internal static partial void ClientJwksFetchFailed(
        this ILogger logger,
        string clientId,
        string jwksUri,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ClientJwksKeyIgnored,
        Level = LogLevel.Debug,
        Message = "A '{KeyType}' key (kid '{KeyId}') in a client's published JSON Web Key Set was ignored: {Reason}"
    )]
    internal static partial void ClientJwksKeyIgnored(
        this ILogger logger,
        string keyType,
        string? keyId,
        string reason
    );

    [LoggerMessage(
        EventId = EventIds.BackChannelLogoutTokenNotProtected,
        Level = LogLevel.Warning,
        Message = "The back-channel logout notification for client '{ClientId}' was not sent because its logout token cannot be signed and encrypted as the client registers."
    )]
    internal static partial void BackChannelLogoutTokenNotProtected(
        this ILogger logger,
        string clientId,
        Exception? exception
    );
}
