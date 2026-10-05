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
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="RevokeTokenCommand"/> message. Only a refresh
/// token (a persisted grant) is revocable, and only by the client it was issued to; everything else is a no-op so the
/// endpoint never reveals token state (RFC 7009).
/// </summary>
internal class DefaultRevokeTokenHandler(
    IPersistedGrantService persistedGrantService,
    IAuditEventRecorder auditEventRecorder,
    TimeProvider timeProvider
) : ICommandHandler<RevokeTokenCommand>, ISupportMediatorPriority
{
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        RevokeTokenCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, request) = command;

        var token = request.Token;
        if (string.IsNullOrEmpty(token))
        {
            // invalid_request
            throw openIdContext
                .ErrorFactory.InvalidRequest(
                    $"The '{OpenIdConstants.Parameters.Token}' parameter is required."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // Only a refresh token is a persisted grant and therefore revocable; a JWT access token is stateless.
        var grantId = PersistedGrantService.CreateGrantId(
            openIdContext.Tenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.RefreshToken,
            token
        );

        var grant = await PersistedGrantService.GetOrDefaultAsync<RefreshTokenGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        // RFC 7009 section 2.1: only the client the token was issued to may revoke it.
        if (
            grant is null
            || !string.Equals(grant.Value.ClientId, openIdClient.ClientId, StringComparison.Ordinal)
        )
        {
            return;
        }

        await PersistedGrantService.SetRevokedOnceAsync(
            openIdContext,
            grantId,
            TimeProvider.GetUtcNow(),
            cancellationToken
        );

        await AuditEventRecorder.RecordTokenRevokedAsync(
            openIdContext,
            openIdClient,
            grant.Value.SubjectId,
            cancellationToken
        );
    }
}
