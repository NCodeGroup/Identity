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
        Message = "Client authentication failed: a stored credential for client '{ClientId}' could not be deserialized (for example, a secret protected by an unavailable master key or root key)."
    )]
    internal static partial void ClientAuthenticationCredentialDeserializationFailed(
        this ILogger logger,
        string clientId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ClientCertificateTrustAnchorsMissing,
        Level = LogLevel.Warning,
        Message = "Client authentication failed: client '{ClientId}' uses tls_client_auth but no trust anchors are configured, so no certificate it presents can be trusted."
    )]
    internal static partial void ClientCertificateTrustAnchorsMissing(
        this ILogger logger,
        string clientId
    );

    [LoggerMessage(
        EventId = EventIds.ClientCertificateUntrusted,
        Level = LogLevel.Information,
        Message = "Client authentication failed: the certificate presented by client '{ClientId}' does not chain to a configured trust anchor for client authentication ({ChainStatus})."
    )]
    internal static partial void ClientCertificateUntrusted(
        this ILogger logger,
        string clientId,
        string chainStatus
    );

    [LoggerMessage(
        EventId = EventIds.ClientCertificateRevocationFailed,
        Level = LogLevel.Information,
        Message = "Client authentication failed: the revocation status of certificate '{Subject}' in the chain presented by client '{ClientId}' is '{Status}'."
    )]
    internal static partial void ClientCertificateRevocationFailed(
        this ILogger logger,
        string clientId,
        string subject,
        string status
    );

    [LoggerMessage(
        EventId = EventIds.ClientCertificateRevocationUnknownAccepted,
        Level = LogLevel.Warning,
        Message = "The revocation status of certificate '{Subject}' in the chain presented by client '{ClientId}' could not be determined, and the client's online_soft_fail revocation mode accepts it."
    )]
    internal static partial void ClientCertificateRevocationUnknownAccepted(
        this ILogger logger,
        string clientId,
        string subject
    );

    [LoggerMessage(
        EventId = EventIds.RevocationInformationUnavailable,
        Level = LogLevel.Warning,
        Message = "The revocation information at '{Location}' could not be used to check certificate '{Subject}': {Reason}"
    )]
    internal static partial void RevocationInformationUnavailable(
        this ILogger logger,
        string location,
        string subject,
        string reason
    );

    [LoggerMessage(
        EventId = EventIds.RevocationRetrievalSuppressed,
        Level = LogLevel.Debug,
        Message = "The revocation information at '{Location}' is not retrieved to check certificate '{Subject}', because a retrieval that failed less than {FailureCacheDuration} ago is remembered."
    )]
    internal static partial void RevocationRetrievalSuppressed(
        this ILogger logger,
        string location,
        string subject,
        TimeSpan failureCacheDuration
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
        EventId = EventIds.EndSessionPostLogoutRedirectUriKindRejected,
        Level = LogLevel.Warning,
        Message = "RP-initiated logout: post_logout_redirect_uri '{PostLogoutRedirectUri}' is registered for client '{ClientId}', but its kind is not permitted by redirect_uri_kinds_supported; not redirecting."
    )]
    internal static partial void EndSessionPostLogoutRedirectUriKindRejected(
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
        EventId = EventIds.SessionAbsoluteExpired,
        Level = LogLevel.Information,
        Message = "End-user session '{SessionId}' expired at the end of its absolute lifetime."
    )]
    internal static partial void SessionAbsoluteExpired(this ILogger logger, string sessionId);

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

    [LoggerMessage(
        EventId = EventIds.ForwardedClientCertificateMalformed,
        Level = LogLevel.Warning,
        Message = "The client certificate forwarded in the '{HeaderName}' header could not be decoded as the configured '{Format}' format; the request is refused. Check that OpenId:MutualTls:Format matches how the proxy encodes the certificate."
    )]
    internal static partial void ForwardedClientCertificateMalformed(
        this ILogger logger,
        string headerName,
        string format
    );

    [LoggerMessage(
        EventId = EventIds.RateLimitExceeded,
        Level = LogLevel.Information,
        Message = "A request to tenant '{TenantId}' exceeded its '{Partition}' limit of {Limit} requests per {Window} and was refused."
    )]
    internal static partial void RateLimitExceeded(
        this ILogger logger,
        string tenantId,
        string partition,
        int limit,
        TimeSpan window
    );

    [LoggerMessage(
        EventId = EventIds.RateLimitStoreUnavailable,
        Level = LogLevel.Warning,
        Message = "The rate-limit counter store did not answer while judging the '{Stage}' limits of tenant '{TenantId}'; rate limits and quotas are not enforced, and every request is admitted, until it answers again. It is tried again every {RetryInterval}."
    )]
    internal static partial void RateLimitStoreUnavailable(
        this ILogger logger,
        string tenantId,
        string stage,
        TimeSpan retryInterval,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.RateLimitStoreRecovered,
        Level = LogLevel.Information,
        Message = "The rate-limit counter store answers again; rate limits and quotas are enforced."
    )]
    internal static partial void RateLimitStoreRecovered(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.TenantUsageFlushFailed,
        Level = LogLevel.Warning,
        Message = "The usage of {EntryCount} tenant-day meters could not be added to the usage ledger; it is kept in memory and added by the next flush."
    )]
    internal static partial void TenantUsageFlushFailed(
        this ILogger logger,
        int entryCount,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TenantUsagePruneFailed,
        Level = LogLevel.Warning,
        Message = "The usage ledger's expired days could not be removed; the removal is retried at the next flush."
    )]
    internal static partial void TenantUsagePruneFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.TenantUsageDiscardedOnStop,
        Level = LogLevel.Warning,
        Message = "The usage of {EntryCount} tenant-day meters could not be added to the usage ledger before the replica stopped and is lost."
    )]
    internal static partial void TenantUsageDiscardedOnStop(this ILogger logger, int entryCount);

    [LoggerMessage(
        EventId = EventIds.GrantPruneFailed,
        Level = LogLevel.Warning,
        Message = "The grants that ended before their retention could not be removed; the removal is retried at the next prune."
    )]
    internal static partial void GrantPruneFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.EndedSessionTerminationFailed,
        Level = LogLevel.Warning,
        Message = "The sessions that ended while unused could not be terminated; the termination is retried at the next pass."
    )]
    internal static partial void EndedSessionTerminationFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.EndedSessionTerminationFailedForTenant,
        Level = LogLevel.Warning,
        Message = "The sessions of tenant {TenantId} that ended while unused could not all be terminated; a session whose grants were not revoked is terminated at the next pass."
    )]
    internal static partial void EndedSessionTerminationFailedForTenant(
        this ILogger logger,
        string tenantId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.EndedSessionsTerminated,
        Level = LogLevel.Information,
        Message = "{Count} sessions that ended while unused were terminated."
    )]
    internal static partial void EndedSessionsTerminated(this ILogger logger, int count);

    [LoggerMessage(
        EventId = EventIds.IncoherentClientReported,
        Level = LogLevel.Warning,
        Message = "The client '{ClientId}' of the tenant '{TenantId}' is incoherent with the server's settings and fails at its next request: {Violations}"
    )]
    internal static partial void IncoherentClientReported(
        this ILogger logger,
        string tenantId,
        string clientId,
        string violations
    );

    [LoggerMessage(
        EventId = EventIds.ClientCoherenceCheckFailed,
        Level = LogLevel.Warning,
        Message = "The clients of the tenant '{TenantId}' could not be checked against the server's settings"
    )]
    internal static partial void ClientCoherenceCheckFailed(
        this ILogger logger,
        string tenantId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ClientCoherenceChecked,
        Level = LogLevel.Information,
        Message = "Checked the stored clients of the tenants that a change to the settings of the server '{ServerId}' affects: {IncoherentClientCount} incoherent"
    )]
    internal static partial void ClientCoherenceChecked(
        this ILogger logger,
        string serverId,
        int incoherentClientCount
    );

    [LoggerMessage(
        EventId = EventIds.PasswordAttemptLimitReached,
        Level = LogLevel.Information,
        Message = "A password for an account of tenant '{TenantId}' was refused: the account reached its limit of {Limit} failed attempts."
    )]
    internal static partial void PasswordAttemptLimitReached(
        this ILogger logger,
        string tenantId,
        int limit
    );

    [LoggerMessage(
        EventId = EventIds.GrantPayloadUnreadable,
        Level = LogLevel.Warning,
        Message = "The payload of a '{GrantType}' grant of tenant '{TenantId}' could not be unprotected, so the grant is treated as absent."
    )]
    internal static partial void GrantPayloadUnreadable(
        this ILogger logger,
        Exception exception,
        string tenantId,
        string grantType
    );
}
