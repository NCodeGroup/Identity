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

using NCode.Identity.Jose;
using NCode.Identity.OpenId.Claims;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Tokens.Commands;

namespace NCode.Identity.OpenId.Tokens.Handlers;

/// <summary>
/// Provides a default implementation for a <see cref="GetAccessTokenSubjectClaimsCommand"/> handler that generates the
/// subject claims for an access token. Custom claims are added by additional handlers provided by the application.
/// </summary>
public class DefaultGetAccessTokenSubjectClaimsHandler(
    IClaimsService claimsService
) : ICommandHandler<GetAccessTokenSubjectClaimsCommand>, ISupportMediatorPriority
{
    private IClaimsService ClaimsService { get; } = claimsService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultOpenIdRegistration.MediatorPriority;

    /// <inheritdoc />
    public ValueTask HandleAsync(GetAccessTokenSubjectClaimsCommand command, CancellationToken cancellationToken)
    {
        var (_, _, tokenContext, targetClaims) = command;
        var (tokenRequest, _, _, _) = tokenContext;

        if (!tokenRequest.SubjectAuthentication.HasValue)
        {
            return ValueTask.CompletedTask;
        }

        var (_, _, sourceClaims, _) = tokenRequest.SubjectAuthentication.Value;

        ClaimsService.CopyClaims(
            sourceClaims,
            targetClaims,
            preventDuplicates: true,
            JoseClaimNames.Payload.Idp,
            JoseClaimNames.Payload.Sub,
            JoseClaimNames.Payload.AuthTime
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
