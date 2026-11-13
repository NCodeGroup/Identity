// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Identity.OpenId.Tenants;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Prepares an uninitialized instance when the host starts: it resolves the control-plane tenant, so that the platform
/// and that tenant exist before the first request, and then ensures that a setup token is available through
/// <see cref="ISetupTokenService.EnsureTokenAsync"/>. An initialized instance needs neither.
/// </summary>
internal sealed class InstanceSetupHostedService(
    IInstanceSetupService instanceSetupService,
    ISetupTokenService setupTokenService,
    ITenantResolver tenantResolver,
    IServiceScopeFactory serviceScopeFactory
) : IHostedService
{
    private IInstanceSetupService InstanceSetupService { get; } = instanceSetupService;

    private ISetupTokenService SetupTokenService { get; } = setupTokenService;

    private ITenantResolver TenantResolver { get; } = tenantResolver;

    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await InstanceSetupService.IsInitializedAsync(cancellationToken))
            return;

        // Resolving the tenant creates the control-plane tenant on demand. The resolver needs an HTTP context only for
        // its request-scoped services, so one is built over a scope of its own, as for other work outside a request.
        await using (var scope = ServiceScopeFactory.CreateAsyncScope())
        {
            var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            await TenantResolver.ResolveControlPlaneTenantAsync(httpContext, cancellationToken);
        }

        await SetupTokenService.EnsureTokenAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
