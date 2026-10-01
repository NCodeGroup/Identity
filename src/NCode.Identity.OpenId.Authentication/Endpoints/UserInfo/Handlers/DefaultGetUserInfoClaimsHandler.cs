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

using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="GetUserInfoClaimsCommand"/> message that supplies
/// the required <c>sub</c> claim. This off-the-shelf handler returns only the subject identifier; applications register
/// additional <see cref="ICommandHandler{GetUserInfoClaimsCommand}"/> handlers to contribute profile, email, and other
/// claims from their own user store (typically gated by the granted scopes).
/// </summary>
internal class DefaultGetUserInfoClaimsHandler
    : ICommandHandler<GetUserInfoClaimsCommand>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        GetUserInfoClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (_, subjectAuthentication, claims) = command;

        // The 'sub' claim is authoritative and always sourced from the authenticated subject.
        claims[JoseClaimNames.Payload.Sub] = subjectAuthentication.SubjectId;

        return ValueTask.CompletedTask;
    }
}
