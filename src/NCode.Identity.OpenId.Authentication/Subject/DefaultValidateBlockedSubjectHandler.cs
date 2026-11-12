// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides a <see cref="ValidateSubjectStandingCommand"/> handler that refuses an end user whose principal an
/// administrator blocked, whatever connection it signed in through, so no session, authorization code or grant of such
/// a principal issues a token. A subject without a principal is left to the other handlers.
/// </summary>
// See ADR-1229.
internal class DefaultValidateBlockedSubjectHandler(
    IStoreManagerFactory storeManagerFactory,
    ILogger<DefaultValidateBlockedSubjectHandler> logger
) : ICommandHandler<ValidateSubjectStandingCommand>
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ILogger<DefaultValidateBlockedSubjectHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateSubjectStandingCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, subjectAuthentication, operationDisposition) = command;

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
