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

            var serverSettings = await EffectiveSettingsResolver.ResolveServerAsync(
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
