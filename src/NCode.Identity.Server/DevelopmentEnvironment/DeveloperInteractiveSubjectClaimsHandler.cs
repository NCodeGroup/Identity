// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Mediator;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// A <strong>development-only</strong> access-token subject-claims handler that projects the interactively-authenticated
/// subject's <c>role</c> claim into the access token, so a seeded administrator user who signs in through the
/// browser flow can exercise the role-gated management API. The default subject-claims projection copies only identity
/// claims (subject, idp, auth time, amr); the production authorization model confers roles through persisted role
/// assignments, so this handler is a local-development bridge and must not be used in production.
/// </summary>
internal sealed class DeveloperInteractiveSubjectClaimsHandler(IClaimsService claimsService)
    : ICommandHandler<GetAccessTokenSubjectClaimsCommand>,
        ISupportMediatorPriority
{
    private IClaimsService ClaimsService { get; } = claimsService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        GetAccessTokenSubjectClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (_, _, tokenContext, subjectClaims) = command;
        var (tokenRequest, _, _, _) = tokenContext;

        if (!tokenRequest.SubjectAuthentication.HasValue)
        {
            return ValueTask.CompletedTask;
        }

        var (_, _, sourceClaims, _) = tokenRequest.SubjectAuthentication.Value;

        ClaimsService.CopyClaims(
            sourceClaims,
            subjectClaims,
            preventDuplicates: false,
            JoseClaimNames.Payload.Role
        );

        return ValueTask.CompletedTask;
    }
}
