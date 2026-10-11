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
