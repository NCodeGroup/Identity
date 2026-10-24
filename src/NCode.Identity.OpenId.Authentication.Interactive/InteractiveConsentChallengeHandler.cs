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
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Handles the <see cref="ConsentChallengeCommand"/> for the interactive surface by redirecting the user-agent to the
/// consent page, carrying the parked continuation as the return URL so the authorization request resumes once the
/// consent decision is recorded. This replaces the core no-op handler when the interactive surface is registered.
/// </summary>
internal sealed class InteractiveConsentChallengeHandler(InteractiveSurfacePaths surfacePaths)
    : ICommandResponseHandler<ConsentChallengeCommand, ReadOnlyEndpointDisposition>
{
    private InteractiveSurfacePaths SurfacePaths { get; } = surfacePaths;

    /// <inheritdoc />
    public ValueTask<ReadOnlyEndpointDisposition> HandleAsync(
        ConsentChallengeCommand command,
        CancellationToken cancellationToken
    )
    {
        var consentPath = SurfacePaths.GetSurfacePath(
            command.OpenIdContext.Http,
            InteractiveAuthenticationDefaults.ConsentPath
        );
        var location = $"{consentPath}?ReturnUrl=" + Uri.EscapeDataString(command.ContinueUrl);

        RedirectHttpResult result = TypedResults.Redirect(location);
        return ValueTask.FromResult(ReadOnlyEndpointDisposition.Handled(result));
    }
}
