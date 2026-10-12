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

using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides the default (no-op) handler for the <see cref="ConsentChallengeCommand"/> message. Without an interactive
/// consent surface there is nothing to redirect to, so the command is left unhandled and the authorization endpoint
/// degrades to an <c>interaction_required</c> error. An interactive surface (for example the opt-in login/consent
/// module) replaces this handler with one that redirects the user-agent to its consent page.
/// </summary>
internal class DefaultConsentChallengeHandler
    : ICommandResponseHandler<ConsentChallengeCommand, ReadOnlyEndpointDisposition>
{
    /// <inheritdoc />
    public ValueTask<ReadOnlyEndpointDisposition> HandleAsync(
        ConsentChallengeCommand command,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(ReadOnlyEndpointDisposition.UnHandled());
}
