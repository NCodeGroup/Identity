// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Endpoints.Continue.Models;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Continue;

/// <summary>
/// Provides a default implementation of the <see cref="IContinueRefusalService"/> abstraction.
/// </summary>
internal class DefaultContinueRefusalService(
    IPersistedGrantService persistedGrantService,
    IContinueProviderSelector continueProviderSelector
) : IContinueRefusalService
{
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IContinueProviderSelector ContinueProviderSelector { get; } = continueProviderSelector;

    /// <inheritdoc />
    public async ValueTask<ReadOnlyEndpointDisposition> RefuseAsync(
        OpenIdContext openIdContext,
        string continueUrl,
        IOpenIdError error,
        CancellationToken cancellationToken
    )
    {
        var state = ContinueUrls.GetStateOrDefault(continueUrl);
        if (string.IsNullOrEmpty(state))
            return ReadOnlyEndpointDisposition.UnHandled();

        var persistedGrantId = PersistedGrantService.CreateGrantId(
            openIdContext.Tenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.Continue,
            state
        );

        // The continuation is spent, so a refused operation cannot be resumed.
        // See ADR-1229.
        var persistedGrantOrNull =
            await PersistedGrantService.ConsumeOnceOrDefault<ContinueEnvelope>(
                openIdContext,
                persistedGrantId,
                cancellationToken
            );
        if (!persistedGrantOrNull.HasValue)
            return ReadOnlyEndpointDisposition.UnHandled();

        var continueEnvelope = persistedGrantOrNull.Value.Payload;
        var provider = ContinueProviderSelector.SelectProvider(continueEnvelope.Code);
        return await provider.RefuseAsync(
            openIdContext,
            continueEnvelope.PayloadJson,
            error,
            cancellationToken
        );
    }
}
