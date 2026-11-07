// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Handlers;

/// <summary>
/// Provides a default implementation for a <see cref="GetAccessTokenSubjectClaimsCommand"/> handler that generates the
/// subject claims for an access token. Custom claims are added by additional handlers provided by the application.
/// With no authenticated subject, the client acts for itself and the subject claim names the client's principal.
/// </summary>
internal class DefaultGetAccessTokenSubjectClaimsHandler(
    IClaimsService claimsService,
    ISubjectIdentifierService subjectIdentifierService
) : ICommandHandler<GetAccessTokenSubjectClaimsCommand>, ISupportMediatorPriority
{
    private IClaimsService ClaimsService { get; } = claimsService;
    private ISubjectIdentifierService SubjectIdentifierService { get; } = subjectIdentifierService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        GetAccessTokenSubjectClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenContext, targetClaims) = command;
        var (tokenRequest, _, _, _) = tokenContext;

        if (!tokenRequest.SubjectAuthentication.HasValue)
        {
            // A client acting for itself is the token's subject: 'sub' names its principal (RFC 9068 §2.2).
            // See ADR-1450.
            targetClaims.Add(new Claim(JoseClaimNames.Payload.Sub, openIdClient.PrincipalId));
            return ValueTask.CompletedTask;
        }

        var subjectAuthentication = tokenRequest.SubjectAuthentication.Value;
        var sourceClaims = subjectAuthentication.Subject;

        // 'sub' is emitted explicitly (projected to the client's public/pairwise identifier), never copied from the
        // source principal, so a pairwise client never sees the stable principal identifier.
        SubjectIdentifierService.AddProjectedSubjectClaim(
            openIdContext,
            openIdClient,
            subjectAuthentication,
            targetClaims
        );

        ClaimsService.CopyClaims(
            sourceClaims,
            targetClaims,
            preventDuplicates: true,
            JoseClaimNames.Payload.Idp,
            JoseClaimNames.Payload.AuthTime,
            JoseClaimNames.Payload.Acr
        );

        ClaimsService.CopyClaims(
            sourceClaims,
            targetClaims,
            preventDuplicates: false,
            JoseClaimNames.Payload.Amr
        );

        return ValueTask.CompletedTask;
    }
}
