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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Management.Contracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Endpoints;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Mediator;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Accounts.Management;

/*

POST   api/tenants/{tenantId}/local-accounts
GET    api/tenants/{tenantId}/local-accounts
GET    api/tenants/{tenantId}/local-accounts/{localAccountId}
PATCH  api/tenants/{tenantId}/local-accounts/{localAccountId}
DELETE api/tenants/{tenantId}/local-accounts/{localAccountId}

POST   api/tenants/{tenantId}/local-accounts/{localAccountId}/enable
POST   api/tenants/{tenantId}/local-accounts/{localAccountId}/disable
POST   api/tenants/{tenantId}/local-accounts/{localAccountId}/password

GET    api/tenants/{tenantId}/local-accounts/{localAccountId}/claims
PUT    api/tenants/{tenantId}/local-accounts/{localAccountId}/claims

GET    api/tenants/{tenantId}/local-accounts/{localAccountId}/metadata
PATCH  api/tenants/{tenantId}/local-accounts/{localAccountId}/metadata

*/

/// <summary>
/// Provides the management endpoints for local accounts. Local accounts are end-user subjects, not ownable resources,
/// so every operation authorizes against the owning tenant scope (there is no per-account owner model). The host must
/// register an <see cref="ILocalAccountStore"/> implementation (the Entity Framework reference or the ASP.NET Core
/// Identity adapter) that these endpoints operate over. Deleting or disabling an account ends every session of its
/// principal.
/// </summary>
internal class LocalAccountApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ICryptoService cryptoService,
    ILocalAccountValidator localAccountValidator,
    IManagementAuditRecorder managementAuditRecorder,
    ISessionService sessionService,
    IOpenIdContextFactory openIdContextFactory
) : ManagementApiEndpointHandler, IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    private ILocalAccountValidator LocalAccountValidator { get; } = localAccountValidator;
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;
    private ISessionService SessionService { get; } = sessionService;
    private IOpenIdContextFactory OpenIdContextFactory { get; } = openIdContextFactory;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var accounts = endpoints
            .MapGroup("/local-accounts")
            .WithTags(OpenIdConstants.EndpointTags.LocalAccounts);

        accounts
            .MapPost("", CreateLocalAccountAsync)
            .Produces<LocalAccountResource>(StatusCodes.Status201Created);
        accounts
            .MapGet("", ListLocalAccountsAsync)
            .Produces<CollectionResource<LocalAccountResource>>();
        accounts.MapGet("/{localAccountId}", GetLocalAccountAsync).Produces<LocalAccountResource>();
        accounts
            .MapPatch("/{localAccountId}", UpdateLocalAccountAsync)
            .Produces(StatusCodes.Status204NoContent);
        accounts
            .MapDelete("/{localAccountId}", DeleteLocalAccountAsync)
            .Produces(StatusCodes.Status204NoContent);

        accounts
            .MapPost("/{localAccountId}/enable", EnableLocalAccountAsync)
            .Produces(StatusCodes.Status204NoContent);
        accounts
            .MapPost("/{localAccountId}/disable", DisableLocalAccountAsync)
            .Produces(StatusCodes.Status204NoContent);
        accounts
            .MapPost("/{localAccountId}/password", ResetPasswordAsync)
            .Produces(StatusCodes.Status204NoContent);

        accounts
            .MapGet("/{localAccountId}/claims", GetClaimsAsync)
            .Produces<LocalAccountClaimsResource>();
        accounts
            .MapPut("/{localAccountId}/claims", SetClaimsAsync)
            .Produces(StatusCodes.Status204NoContent);

        accounts
            .MapGet("/{localAccountId}/metadata", GetMetadataAsync)
            .Produces<LocalAccountMetadataResource>();
        accounts
            .MapPatch("/{localAccountId}/metadata", SetMetadataAsync)
            .Produces(StatusCodes.Status204NoContent);
    }

    internal virtual LocalAccountResource ToLocalAccountResource(PersistedLocalAccount account) =>
        new()
        {
            TenantId = account.TenantId,
            LocalAccountId = account.LocalAccountId,
            UserName = account.UserName,
            Email = account.Email,
            EmailVerified = account.EmailVerified,
            IsEnabled = account.IsEnabled,
            ConcurrencyToken = account.ConcurrencyToken,
        };

    internal virtual async ValueTask<IResult> CreateLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] CreateLocalAccountRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var error = await LocalAccountValidator.ValidateCreateAsync(
            httpContext.User,
            tenantId,
            request.UserName,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        var localAccountId = CryptoService.GenerateResourceId();
        var principalId = CryptoService.GenerateResourceId();

        // Eagerly create the owning principal and the account's self-issued connection so the account resolves to a
        // principal uniformly with any federated identity (ADR-0051/ADR-0057).
        var principal = new PersistedFederatedPrincipal
        {
            TenantId = tenantId,
            PrincipalId = principalId,
            ConcurrencyToken = string.Empty,
        };

        // Set-when-present: a missing bag leaves the principal's default empty object (ADR-0055).
        if (request.ProfileMetadata is { } profileMetadata)
        {
            principal.ProfileMetadata = profileMetadata;
        }

        if (request.SystemMetadata is { } systemMetadata)
        {
            principal.SystemMetadata = systemMetadata;
        }

        await storeManager
            .GetStore<IFederatedPrincipalStore>()
            .AddAsync(principal, cancellationToken);

        var identity = new PersistedFederatedIdentity
        {
            TenantId = tenantId,
            FederatedIdentityId = CryptoService.GenerateResourceId(),
            PrincipalId = principalId,
            Issuer = AccountConstants.SelfIssuer,
            Subject = localAccountId,
            JoinKey = null,
            ConcurrencyToken = string.Empty,
        };
        await storeManager
            .GetStore<IFederatedIdentityStore>()
            .AddAsync(identity, cancellationToken);

        var account = new PersistedLocalAccount
        {
            TenantId = tenantId,
            LocalAccountId = localAccountId,
            UserName = request.UserName,
            Email = request.Email,
            EmailVerified = request.EmailVerified,
            IsEnabled = request.IsEnabled,
            Claims =
                request
                    .Claims?.Select(claim => new PersistedLocalAccountClaim
                    {
                        Type = claim.Type,
                        Value = claim.Value,
                    })
                    .ToList()
                ?? [],
            ConcurrencyToken = string.Empty,
        };

        await WithPasswordBytesAsync(
            request.Password,
            (bytes, ct) => storeManager.GetStore<ILocalAccountStore>().AddAsync(account, bytes, ct),
            cancellationToken
        );

        await storeManager.SaveChangesAsync(cancellationToken);

        var resource = ToLocalAccountResource(account);

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Created,
            ToResourceValues(resource),
            cancellationToken
        );

        httpContext.Response.Headers.ETag = account.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/local-accounts/{localAccountId}",
            resource
        );
    }

    internal virtual async ValueTask<IResult> ListLocalAccountsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        return await ProcessListAsync(
            httpContext,
            TenantScopeResource.For(tenantId),
            () =>
                store.GetPageAsync(
                    new PageRequest { Cursor = cursor, Limit = NormalizeLimit(limit) },
                    cancellationToken
                ),
            ToLocalAccountResource
        );
    }

    internal virtual async ValueTask<IResult> GetLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var account = await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByIdOrDefaultAsync(localAccountId, cancellationToken);

        return await ProcessGetAsync(
            httpContext,
            account,
            TenantScopeResource.For(tenantId),
            Operations.Read,
            ToLocalAccountResource
        );
    }

    internal virtual async ValueTask<IResult> UpdateLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] UpdateLocalAccountRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        var account = await store.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
        var error = await LocalAccountValidator.ValidateUpdateAsync(
            httpContext.User,
            tenantId,
            localAccountId,
            request.UserName,
            account.ConcurrencyToken,
            string.IsNullOrEmpty(ifMatch) ? null : ifMatch,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        account.UserName = request.UserName;
        account.Email = request.Email;
        account.EmailVerified = request.EmailVerified;
        await store.UpdateAsync(account, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToLocalAccountResource(account)),
            cancellationToken
        );

        httpContext.Response.Headers.ETag = account.ConcurrencyToken;
        return TypedResults.NoContent();
    }

    internal virtual async ValueTask<IResult> DeleteLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var accountStore = storeManager.GetStore<ILocalAccountStore>();
        var account = await accountStore.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var error = await LocalAccountValidator.ValidateDeleteAsync(
            httpContext.User,
            tenantId,
            localAccountId,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        // Cascade the account's self-issued connection, and the owning principal when this was its only identity.
        var identityStore = storeManager.GetStore<IFederatedIdentityStore>();
        var identity = await identityStore.GetByIssuerSubjectAsync(
            AccountConstants.SelfIssuer,
            localAccountId,
            cancellationToken
        );
        if (identity is not null)
        {
            // The principal's sessions end before the deletion commits, while they can still be listed to notify their relying parties. See ADR-0224.
            await TerminateSessionsAsync(
                httpContext,
                tenantId,
                identity.PrincipalId,
                SessionTerminationReasons.AccountDeleted,
                cancellationToken
            );
        }

        await accountStore.RemoveAsync(localAccountId, cancellationToken);

        if (identity is not null)
        {
            await identityStore.RemoveAsync(identity.FederatedIdentityId, cancellationToken);

            var siblings = await identityStore.GetByPrincipalAsync(
                identity.PrincipalId,
                cancellationToken
            );
            if (
                siblings.All(sibling => sibling.FederatedIdentityId == identity.FederatedIdentityId)
            )
            {
                await storeManager
                    .GetStore<IFederatedPrincipalStore>()
                    .RemoveAsync(identity.PrincipalId, cancellationToken);
            }
        }

        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Deleted,
            resourceValues: null,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    internal virtual ValueTask<IResult> EnableLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    ) => SetEnabledAsync(httpContext, tenantId, localAccountId, isEnabled: true, cancellationToken);

    internal virtual ValueTask<IResult> DisableLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    ) =>
        SetEnabledAsync(httpContext, tenantId, localAccountId, isEnabled: false, cancellationToken);

    private async ValueTask<IResult> SetEnabledAsync(
        HttpContext httpContext,
        string tenantId,
        string localAccountId,
        bool isEnabled,
        CancellationToken cancellationToken
    )
    {
        if (!await AuthorizeTenantAsync(httpContext, tenantId, Operations.Update))
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        var account = await store.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        account.IsEnabled = isEnabled;
        await store.UpdateAsync(account, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        if (!isEnabled)
        {
            var identity = await storeManager
                .GetStore<IFederatedIdentityStore>()
                .GetByIssuerSubjectAsync(
                    AccountConstants.SelfIssuer,
                    localAccountId,
                    cancellationToken
                );
            if (identity is not null)
            {
                // A disabled account's sessions end, which revokes every grant of its principal and notifies its relying parties. See ADR-0224.
                await TerminateSessionsAsync(
                    httpContext,
                    tenantId,
                    identity.PrincipalId,
                    SessionTerminationReasons.AccountDisabled,
                    cancellationToken
                );
            }
        }

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToLocalAccountResource(account)),
            cancellationToken
        );

        httpContext.Response.Headers.ETag = account.ConcurrencyToken;
        return TypedResults.NoContent();
    }

    internal virtual async ValueTask<IResult> ResetPasswordAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] ResetLocalAccountPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!await AuthorizeTenantAsync(httpContext, tenantId, Operations.Update))
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        var account = await store.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        await WithPasswordBytesAsync(
            request.Password,
            (bytes, ct) => store.SetPasswordAsync(localAccountId, bytes, ct),
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Updated,
            resourceValues: null,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    internal virtual async ValueTask<IResult> GetClaimsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var account = await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByIdOrDefaultAsync(localAccountId, cancellationToken);

        return await ProcessGetAsync(
            httpContext,
            account,
            TenantScopeResource.For(tenantId),
            Operations.Read,
            value => ToClaimsResource(tenantId, value)
        );
    }

    internal virtual async ValueTask<IResult> SetClaimsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] SetLocalAccountClaimsRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!await AuthorizeTenantAsync(httpContext, tenantId, Operations.Update))
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        var account = await store.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var claims = request
            .Claims.Select(claim => new PersistedLocalAccountClaim
            {
                Type = claim.Type,
                Value = claim.Value,
            })
            .ToList();
        await store.ReplaceClaimsAsync(localAccountId, claims, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Updated,
            resourceValues: null,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    internal virtual async ValueTask<IResult> GetMetadataAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var principal = await ResolvePrincipalAsync(
            storeManager,
            localAccountId,
            cancellationToken
        );

        return await ProcessGetAsync(
            httpContext,
            principal,
            TenantScopeResource.For(tenantId),
            Operations.Read,
            value => ToMetadataResource(tenantId, localAccountId, value)
        );
    }

    internal virtual async ValueTask<IResult> SetMetadataAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] SetLocalAccountMetadataRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!await AuthorizeTenantAsync(httpContext, tenantId, Operations.Update))
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var principalStore = storeManager.GetStore<IFederatedPrincipalStore>();

        var principal = await ResolvePrincipalAsync(
            storeManager,
            localAccountId,
            cancellationToken
        );
        if (principal is null)
        {
            return TypedResults.NotFound();
        }

        // Set-when-present: a missing bag leaves the current value untouched.
        if (request.ProfileMetadata is { } profileMetadata)
        {
            principal.ProfileMetadata = profileMetadata;
        }

        if (request.SystemMetadata is { } systemMetadata)
        {
            principal.SystemMetadata = systemMetadata;
        }

        await principalStore.UpdateAsync(principal, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Updated,
            resourceValues: null,
            cancellationToken
        );

        httpContext.Response.Headers.ETag = principal.ConcurrencyToken;
        return TypedResults.NoContent();
    }

    private static LocalAccountClaimsResource ToClaimsResource(
        string tenantId,
        PersistedLocalAccount account
    ) =>
        new()
        {
            TenantId = tenantId,
            LocalAccountId = account.LocalAccountId,
            Claims = account
                .Claims.Select(claim => new LocalAccountClaim
                {
                    Type = claim.Type,
                    Value = claim.Value,
                })
                .ToList(),
            ConcurrencyToken = account.ConcurrencyToken,
        };

    private static LocalAccountMetadataResource ToMetadataResource(
        string tenantId,
        string localAccountId,
        PersistedFederatedPrincipal principal
    ) =>
        new()
        {
            TenantId = tenantId,
            LocalAccountId = localAccountId,
            PrincipalId = principal.PrincipalId,
            ProfileMetadata = principal.ProfileMetadata,
            SystemMetadata = principal.SystemMetadata,
            ConcurrencyToken = principal.ConcurrencyToken,
        };

    private static async ValueTask<PersistedFederatedPrincipal?> ResolvePrincipalAsync(
        IStoreManager storeManager,
        string localAccountId,
        CancellationToken cancellationToken
    )
    {
        var identity = await storeManager
            .GetStore<IFederatedIdentityStore>()
            .GetByIssuerSubjectAsync(
                AccountConstants.SelfIssuer,
                localAccountId,
                cancellationToken
            );
        if (identity is null)
        {
            return null;
        }

        return await storeManager
            .GetStore<IFederatedPrincipalStore>()
            .GetOrDefaultAsync(identity.PrincipalId, cancellationToken);
    }

    // Materializes a context for the addressed tenant so session termination runs the seam (grant revocation,
    // back-channel logout, auditing) in that tenant's security domain rather than the request's resolved tenant.
    private async ValueTask TerminateSessionsAsync(
        HttpContext httpContext,
        string tenantId,
        string principalId,
        string reason,
        CancellationToken cancellationToken
    )
    {
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        await using var tenantContext = await OpenIdContextFactory.CreateForTenantAsync(
            httpContext,
            tenantId,
            mediator,
            cancellationToken
        );

        await SessionService.RevokeAllAsync(tenantContext, principalId, reason, cancellationToken);
    }

    private async ValueTask<bool> AuthorizeTenantAsync(
        HttpContext httpContext,
        string tenantId,
        IAuthorizationRequirement operation
    )
    {
        var result = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantScopeResource.For(tenantId),
            operation
        );
        return result.Succeeded;
    }

    // Encodes the password into a pinned, zeroed-on-return buffer so the plaintext never lingers on the GC heap.
    private static async ValueTask WithPasswordBytesAsync(
        string password,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> useAsync,
        CancellationToken cancellationToken
    )
    {
        var byteCount = SecureEncoding.UTF8.GetByteCount(password);
        using var owner = SecureMemoryPool<byte>.Shared.Rent(byteCount);
        var written = SecureEncoding.UTF8.GetBytes(password, owner.Memory.Span);
        await useAsync(owner.Memory[..written], cancellationToken);
    }
}
