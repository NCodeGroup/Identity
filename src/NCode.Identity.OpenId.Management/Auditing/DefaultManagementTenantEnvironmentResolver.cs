// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Provides the default implementation of <see cref="IManagementTenantEnvironmentResolver"/>: it reads the tenant's
/// committed settings document and resolves it under the server's effective settings, as the tenant endpoints do, and
/// memoizes the result for the request. A tenant whose settings cannot be read yields <see langword="null"/>, because an
/// audit record for a committed write is published regardless.
/// </summary>
internal class DefaultManagementTenantEnvironmentResolver(
    IStoreManagerFactory storeManagerFactory,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    ILogger<DefaultManagementTenantEnvironmentResolver> logger
) : IManagementTenantEnvironmentResolver
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private ILogger<DefaultManagementTenantEnvironmentResolver> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<string?> GetEnvironmentTypeAsync(
        HttpContext httpContext,
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(tenantId);

        if (ManagementTenantEnvironment.TryRecall(httpContext, tenantId, out var remembered))
            return remembered;

        string environmentType;
        try
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<ITenantStore>();
            var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
            if (tenant is null)
                return null;

            var serverSettings = await EffectiveSettingsResolver.ResolvePlatformAsync(
                cancellationToken
            );
            var settings = EffectiveSettingsResolver.ResolveStored(
                serverSettings,
                OpenIdSettingScopes.Tenant,
                tenantId,
                tenant.Settings.Value
            );
            environmentType = settings.GetValue(OpenIdSettingKeys.EnvironmentType);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.TenantEnvironmentTypeUnreadable(tenantId, exception);
            return null;
        }

        ManagementTenantEnvironment.Remember(httpContext, tenantId, environmentType);
        return environmentType;
    }
}
