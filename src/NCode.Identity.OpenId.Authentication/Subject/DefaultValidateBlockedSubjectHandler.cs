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

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides a <see cref="ValidateSubjectAuthenticationCommand"/> handler that refuses a subject whose principal an
/// administrator blocked, whatever connection it signed in through, so a session, an authorization code or a grant of
/// such a principal issues no token. A subject without a principal is left to the other handlers.
/// </summary>
// See ADR-0226.
internal class DefaultValidateBlockedSubjectHandler(
    IStoreManagerFactory storeManagerFactory,
    ILogger<DefaultValidateBlockedSubjectHandler> logger
) : ICommandHandler<ValidateSubjectAuthenticationCommand>
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ILogger<DefaultValidateBlockedSubjectHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateSubjectAuthenticationCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, _, _, subjectAuthentication, operationDisposition, _) = command;

        // short-circuit if we already have an error
        if (operationDisposition.HasError)
        {
            return;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var principal = await storeManager
            .GetStore<IFederatedPrincipalStore>()
            .GetOrDefaultAsync(subjectAuthentication.PrincipalId, cancellationToken);

        if (principal is { IsBlocked: true })
        {
            const string message = "The end-user is blocked.";
            operationDisposition.Error ??= openIdContext.ErrorFactory.AccessDenied(message);
            Logger.SubjectValidationFailed(message);
        }
    }
}
