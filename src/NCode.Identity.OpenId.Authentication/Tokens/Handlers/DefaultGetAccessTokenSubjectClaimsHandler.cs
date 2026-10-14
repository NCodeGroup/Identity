#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using NCode.Identity.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Handlers;

/// <summary>
/// Provides a default implementation for a <see cref="GetAccessTokenSubjectClaimsCommand"/> handler that generates the
/// subject claims for an access token. Custom claims are added by additional handlers provided by the application.
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
