#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Ciba;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenGrantHandler"/> for the <c>CIBA</c> grant type
/// (OpenID Connect Client-Initiated Backchannel Authentication §10–11), polled by the client with its
/// <c>auth_req_id</c>.
/// </summary>
internal class DefaultCibaGrantHandler(
    TimeProvider timeProvider,
    IPersistedGrantService persistedGrantService,
    ICibaTokenResponseService tokenResponseService
) : ITokenGrantHandler
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private ICibaTokenResponseService TokenResponseService { get; } = tokenResponseService;

    /// <inheritdoc />
    public IReadOnlySet<string> GrantTypes { get; } =
        new HashSet<string>(StringComparer.Ordinal) { OpenIdConstants.GrantTypes.Ciba };

    /// <inheritdoc />
    public async ValueTask<IOpenIdResponse> HandleAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        ProofOfPossessionBinding proofOfPossessionBinding,
        CancellationToken cancellationToken
    )
    {
        var tenantId = openIdContext.Tenant.TenantId;
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        var authReqId = tokenRequest.AuthReqId;
        if (string.IsNullOrEmpty(authReqId))
            return errorFactory
                .MissingParameter(OpenIdConstants.Parameters.AuthReqId)
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var grantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.Ciba,
            authReqId
        );

        var persistedGrantOrNull = await PersistedGrantService.GetOrDefaultAsync<CibaGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        if (!persistedGrantOrNull.HasValue)
            return errorFactory
                .InvalidGrant("The provided auth_req_id is invalid.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var persistedGrant = persistedGrantOrNull.Value;

        // An auth_req_id is redeemed only by the client it was issued to (CIBA Core §11). See ADR-0225.
        if (
            !string.Equals(
                persistedGrant.Payload.ClientId,
                openIdClient.ClientId,
                StringComparison.Ordinal
            )
        )
            return errorFactory
                .InvalidGrant("The provided auth_req_id was issued to a different client.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        if (persistedGrant.Status == PersistedGrantStatus.Expired)
            return Error(
                errorFactory,
                OpenIdProtocol.ErrorCodes.ExpiredToken,
                "The backchannel authentication request has expired."
            );

        if (persistedGrant.Status != PersistedGrantStatus.Active)
            return errorFactory
                .InvalidGrant("The provided auth_req_id is invalid, expired, or revoked.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var payload = persistedGrant.Payload;

        // Push delivery pushes the tokens inside the client notification at approval time; the client never polls the
        // token endpoint for a push-mode request (CIBA §10.3).
        if (
            string.Equals(
                payload.DeliveryMode,
                OpenIdConstants.BackchannelTokenDeliveryModes.Push,
                StringComparison.Ordinal
            )
        )
            return errorFactory
                .Create(OpenIdProtocol.ErrorCodes.UnauthorizedClient)
                .WithDescription(
                    "The token endpoint cannot be used with push token delivery; the tokens are delivered to the client notification endpoint."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest);

        switch (payload.Status)
        {
            case DeviceAuthorizationStatus.Denied:
                return Error(
                    errorFactory,
                    OpenIdProtocol.ErrorCodes.AccessDenied,
                    "The end user denied the backchannel authentication request."
                );

            case DeviceAuthorizationStatus.Pending:
                return await HandlePendingAsync(
                    openIdContext,
                    errorFactory,
                    settings,
                    grantId,
                    payload,
                    cancellationToken
                );

            case DeviceAuthorizationStatus.Approved:
                return await HandleApprovedAsync(
                    openIdContext,
                    openIdClient,
                    errorFactory,
                    tokenRequest,
                    grantId,
                    payload,
                    proofOfPossessionBinding,
                    cancellationToken
                );

            default:
                return errorFactory
                    .InvalidGrant("The provided auth_req_id is invalid.")
                    .WithStatusCode(StatusCodes.Status400BadRequest);
        }
    }

    private async ValueTask<IOpenIdResponse> HandlePendingAsync(
        OpenIdContext openIdContext,
        IOpenIdErrorFactory errorFactory,
        IReadOnlySettingCollection settings,
        PersistedGrantId grantId,
        CibaGrant payload,
        CancellationToken cancellationToken
    )
    {
        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var interval = settings.GetValue(OpenIdSettingKeys.CibaPollingInterval);

        var tooFast =
            payload.LastPolledWhen is { } lastPolledWhen && utcNow - lastPolledWhen < interval;

        await PersistedGrantService.UpdatePayloadAsync<CibaGrant>(
            openIdContext,
            grantId,
            stored => RecordPoll(stored, utcNow),
            cancellationToken
        );

        return tooFast
            ? Error(
                errorFactory,
                OpenIdProtocol.ErrorCodes.SlowDown,
                "The client is polling faster than the permitted interval."
            )
            : Error(
                errorFactory,
                OpenIdProtocol.ErrorCodes.AuthorizationPending,
                "The end user has not yet completed the backchannel authentication request."
            );
    }

    private static PersistedGrantUpdate<CibaGrant>? RecordPoll(
        PersistedGrant<CibaGrant> stored,
        DateTimeOffset utcNow
    )
    {
        // An approval that lands between this poll's read and its write stands. See ADR-0222.
        if (stored.Payload.Status != DeviceAuthorizationStatus.Pending)
            return null;

        return new PersistedGrantUpdate<CibaGrant>
        {
            Payload = stored.Payload with { LastPolledWhen = utcNow },
        };
    }

    private async ValueTask<IOpenIdResponse> HandleApprovedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdErrorFactory errorFactory,
        ITokenRequest tokenRequest,
        PersistedGrantId grantId,
        CibaGrant payload,
        ProofOfPossessionBinding proofOfPossessionBinding,
        CancellationToken cancellationToken
    )
    {
        // The subject that approved the request is validated before the auth_req_id is spent, so a refused subject
        // leaves it unspent. See ADR-0225.
        var validateCommand = new ValidateTokenGrantCommand<CibaGrant>(
            openIdContext,
            openIdClient,
            tokenRequest,
            payload
        );
        await openIdContext.Mediator.SendAsync(validateCommand, cancellationToken);

        // Claim the approved request exactly once so an auth_req_id is redeemed for tokens a single time.
        var consumedOrNull = await PersistedGrantService.ConsumeOnceOrDefault<CibaGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        if (!consumedOrNull.HasValue)
            return errorFactory
                .InvalidGrant("The provided auth_req_id has already been redeemed.")
                .WithStatusCode(StatusCodes.Status400BadRequest);

        var consumedPayload = consumedOrNull.Value.Payload;

        return await TokenResponseService.CreateTokenResponseAsync(
            openIdContext,
            openIdClient,
            consumedPayload,
            proofOfPossessionBinding,
            cancellationToken
        );
    }

    private static IOpenIdError Error(
        IOpenIdErrorFactory errorFactory,
        string errorCode,
        string description
    ) =>
        errorFactory
            .Create(errorCode)
            .WithDescription(description)
            .WithStatusCode(StatusCodes.Status400BadRequest);
}
