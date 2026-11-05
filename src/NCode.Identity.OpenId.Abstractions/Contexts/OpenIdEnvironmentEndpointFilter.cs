// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Results;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Contexts;

/// <summary>
/// The shared endpoint filter that materializes the OpenID request environment for every OpenID endpoint: it builds the
/// <see cref="OpenIdContext"/> (the platform and the resolved, settings/secrets-bearing tenant), publishes it on the
/// request as an <see cref="IOpenIdContextFeature"/> so endpoints and services can retrieve it, and opens the ambient
/// tenant scope that tenant-scoped data access reads. Installing this once per endpoint group means the authentication
/// (protocol) endpoints and the management endpoints run the exact same environment pipeline.
/// </summary>
/// <remarks>
/// An exception that a registered <see cref="IOpenIdExceptionTranslator"/> answers while the context is created (such
/// as a tenant whose root key is unavailable, whose secrets cannot be unwrapped) is answered with that protocol error.
/// No context exists yet, so the error is not audited: the exception's owner logs and audits the outage.
/// </remarks>
// See ADR-1428.
[PublicAPI]
public sealed class OpenIdEnvironmentEndpointFilter(
    IOpenIdContextFactory openIdContextFactory,
    IAmbientTenantAccessor ambientTenantAccessor,
    IOpenIdEnvironmentProvider openIdEnvironmentProvider,
    IEnumerable<IOpenIdExceptionTranslator> exceptionTranslators
) : IEndpointFilter
{
    private IOpenIdContextFactory OpenIdContextFactory { get; } = openIdContextFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } =
        openIdEnvironmentProvider;
    private IOpenIdExceptionTranslator[] ExceptionTranslators { get; } = [.. exceptionTranslators];

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();

        OpenIdContext openIdContext;
        try
        {
            // The factory publishes the context on the request (an IOpenIdContextFeature) for endpoints to retrieve.
            openIdContext = await OpenIdContextFactory.CreateForRequestAsync(
                httpContext,
                mediator,
                httpContext.RequestAborted
            );
        }
        catch (HttpResultException exception)
        {
            // An unresolvable tenant (unknown host/path) is reported as its mapped result (typically 404). The ambient
            // scope opens only below, so tenant/platform listings on central-admin surfaces are never tenant-restricted.
            return exception.HttpResult;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException
                && TryTranslate(exception, out var error)
            )
        {
            // The tenant's context cannot be created (such as a root key that is unavailable), so the translator's
            // protocol error answers the request. No context exists to audit the error against.
            return error.AsHttpResult();
        }

        using var scope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);
        return await next(context);
    }

    private bool TryTranslate(Exception exception, [NotNullWhen(true)] out IOpenIdError? error)
    {
        var openIdEnvironment = OpenIdEnvironmentProvider.Get();
        foreach (var translator in ExceptionTranslators)
        {
            if (translator.TryTranslate(openIdEnvironment, exception, out error))
                return true;
        }

        error = null;
        return false;
    }
}
