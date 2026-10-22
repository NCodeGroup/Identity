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

namespace NCode.Identity.OpenId.Authentication.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.OpenId.Authentication</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>2000</c>–<c>2999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 2000;

    public const int AuthorizationRequestNotHandled = Base + 1;
    public const int ClientRequestedAccountCreation = Base + 2;
    public const int ClientRequestedReAuthentication = Base + 3;
    public const int FailedToDecodeJwt = Base + 4;
    public const int FailedToDeserializeJson = Base + 5;
    public const int FailedToFetchRequestUri = Base + 6;
    public const int MissingStateParameter = Base + 7;
    public const int InvalidStateParameter = Base + 8;
    public const int ContinueProviderNotHandled = Base + 9;
    public const int PasswordGrantNotSupported = Base + 10;
    public const int SubjectValidationFailed = Base + 11;
    public const int ClientAuthenticationClientNotFound = Base + 12;
    public const int ClientAuthenticationCredentialDeserializationFailed = Base + 13;
    public const int ClientAuthenticationSecretMismatch = Base + 14;
    public const int TokenRequestClientAuthenticationRequired = Base + 15;
    public const int OpenIdErrorAuditFailed = Base + 16;
    public const int UnexpectedOpenIdError = Base + 17;
    public const int OpenIdErrorException = Base + 18;

    // Base + 19 is retired; an event ID is never reused.
    public const int AccessTokenInvalid = Base + 20;
    public const int EndUserSessionAuthenticationFailed = Base + 21;
    public const int EndSessionIdTokenHintInvalid = Base + 22;
    public const int EndSessionSubjectUnresolved = Base + 23;
    public const int EndSessionPostLogoutRedirectUriRejected = Base + 24;
    public const int EndSessionCompleted = Base + 25;
    public const int ConsentRequired = Base + 26;
    public const int SessionCreated = Base + 27;
    public const int SessionIdleExpired = Base + 28;
    public const int SessionEvicted = Base + 29;
    public const int EndUserSessionExpired = Base + 30;
    public const int ClientAssertionInvalid = Base + 31;
    public const int ClientAssertionReplayed = Base + 32;
    public const int ClientJwksFetchFailed = Base + 33;
    public const int ClientAuthenticationMethodNotRegistered = Base + 35;
    public const int ClientAuthenticationMethodNotPermitted = Base + 36;
    public const int BackChannelLogoutTokenNotProtected = Base + 37;
    public const int ClientJwksKeyIgnored = Base + 38;
    public const int ForwardedClientCertificateMalformed = Base + 39;
    public const int ClientCertificateTrustAnchorsMissing = Base + 40;
    public const int ClientCertificateUntrusted = Base + 41;
    public const int RateLimitExceeded = Base + 42;
    public const int RateLimitStoreUnavailable = Base + 43;
    public const int QuotaExceeded = Base + 44;
    public const int TenantUsageFlushFailed = Base + 45;
    public const int TenantUsagePruneFailed = Base + 46;
    public const int RateLimitStoreRecovered = Base + 47;
    public const int EndSessionPostLogoutRedirectUriKindRejected = Base + 48;
}
