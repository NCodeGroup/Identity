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

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NCode.Identity.Events.Audit;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.LocalAccounts;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.LocalAccounts;

/*

POST   api/tenants/{tenantId}/local-accounts
GET    api/tenants/{tenantId}/local-accounts
GET    api/tenants/{tenantId}/local-accounts/{localAccountId}
PATCH  api/tenants/{tenantId}/local-accounts/{localAccountId}
DELETE api/tenants/{tenantId}/local-accounts/{localAccountId}

POST   api/tenants/{tenantId}/local-accounts/{localAccountId}/disable
POST   api/tenants/{tenantId}/local-accounts/{localAccountId}/enable
POST   api/tenants/{tenantId}/local-accounts/{localAccountId}/password

GET    api/tenants/{tenantId}/local-accounts/{localAccountId}/metadata
PATCH  api/tenants/{tenantId}/local-accounts/{localAccountId}/metadata

GET    api/tenants/{tenantId}/local-accounts/{localAccountId}/owners
POST   api/tenants/{tenantId}/local-accounts/{localAccountId}/owners
DELETE api/tenants/{tenantId}/local-accounts/{localAccountId}/owners/{principalId}

*/

/// <summary>
/// Provides the API endpoints for managing local accounts, including their profile, enabled status, credential, and
/// server-owned metadata. The endpoints require a host to have registered an <see cref="ILocalAccountProvisioner"/>
/// (the opt-in local-account persistence package); without one they respond <c>501 Not Implemented</c>.
/// </summary>
internal class LocalAccountApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ILocalAccountValidator localAccountValidator,
    ICryptoService cryptoService,
    IResourceOwnershipService resourceOwnershipService,
    IManagementAuditRecorder managementAuditRecorder,
    ILogger<LocalAccountApiEndpointHandler> logger,
    ILocalAccountProvisioner? localAccountProvisioner = null
) : BaseOwnableApiEndpointHandler, IEndpointProvider
{
    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    protected override IResourceOwnershipService ResourceOwnershipService { get; } =
        resourceOwnershipService;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    private ILocalAccountValidator LocalAccountValidator { get; } = localAccountValidator;
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;
    private ILogger<LocalAccountApiEndpointHandler> Logger { get; } = logger;
    private ILocalAccountProvisioner? LocalAccountProvisioner { get; } = localAccountProvisioner;

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
            .MapPost("/{localAccountId}/disable", DisableLocalAccountAsync)
            .Produces(StatusCodes.Status204NoContent);
        accounts
            .MapPost("/{localAccountId}/enable", EnableLocalAccountAsync)
            .Produces(StatusCodes.Status204NoContent);
        accounts
            .MapPost("/{localAccountId}/password", ResetPasswordAsync)
            .Produces(StatusCodes.Status204NoContent);

        accounts
            .MapGet("/{localAccountId}/metadata", GetMetadataAsync)
            .Produces<LocalAccountMetadataResource>();
        accounts
            .MapPatch("/{localAccountId}/metadata", SetMetadataAsync)
            .Produces(StatusCodes.Status204NoContent);

        accounts
            .MapGet("/{localAccountId}/owners", ListOwnersAsync)
            .Produces<CollectionResource<OwnerResource>>();
        accounts
            .MapPost("/{localAccountId}/owners", AddOwnerAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created);
        accounts
            .MapDelete("/{localAccountId}/owners/{principalId}", RemoveOwnerAsync)
            .Produces(StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Maps a <see cref="PersistedLocalAccount"/> to its <see cref="LocalAccountResource"/> representation.
    /// </summary>
    /// <param name="account">The <see cref="PersistedLocalAccount"/> to map.</param>
    /// <returns>The mapped <see cref="LocalAccountResource"/>.</returns>
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

    /// <summary>
    /// Maps an account's owning <see cref="PersistedFederatedPrincipal"/> to its
    /// <see cref="LocalAccountMetadataResource"/> representation.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant the account belongs to.</param>
    /// <param name="localAccountId">The identifier of the local account.</param>
    /// <param name="principal">The account's owning principal.</param>
    /// <returns>The mapped <see cref="LocalAccountMetadataResource"/>.</returns>
    internal virtual LocalAccountMetadataResource ToLocalAccountMetadataResource(
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

    /// <summary>
    /// Lists the local accounts in a tenant.
    /// </summary>
    /// <response code="200">A page of local accounts.</response>
    [EndpointName("api/local-accounts/list")]
    internal virtual async ValueTask<IResult> ListLocalAccountsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is null)
        {
            return LocalAccountsNotConfigured();
        }

        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            new TenantScopeResource(tenantId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<ILocalAccountStore>();
                return await store.GetPageAsync(cursor, effectiveLimit, cancellationToken);
            },
            ToLocalAccountResource
        );
    }

    /// <summary>
    /// Gets a local account.
    /// </summary>
    /// <response code="200">The requested local account.</response>
    [EndpointName("api/local-accounts/get")]
    internal virtual async ValueTask<IResult> GetLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is null)
        {
            return LocalAccountsNotConfigured();
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var account = await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByIdOrDefaultAsync(localAccountId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.LocalAccount, localAccountId);
        return await ProcessGetAsync(
            httpContext,
            account,
            node,
            Operations.Read,
            ToLocalAccountResource
        );
    }

    /// <summary>
    /// Creates a local account.
    /// </summary>
    /// <response code="201">The created local account.</response>
    [EndpointName("api/local-accounts/create")]
    internal virtual async ValueTask<IResult> CreateLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] CreateLocalAccountRequest request,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is not { } provisioner)
        {
            return LocalAccountsNotConfigured();
        }

        var openIdContext = httpContext.GetOpenIdContext();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        // Core preconditions (authorization + username uniqueness) are asserted by the validator (ADR-0014).
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

        var localAccountId = await WithEncodedPasswordAsync(
            request.Password,
            password =>
                provisioner.CreateAsync(
                    openIdContext,
                    storeManager,
                    new LocalAccountCreationRequest
                    {
                        UserName = request.UserName,
                        Password = password,
                        Email = request.Email,
                        EmailVerified = request.EmailVerified,
                        IsEnabled = request.IsEnabled,
                    },
                    cancellationToken
                )
        );

        await ResourceOwnershipService.AssignCreatorAsync(
            openIdContext,
            httpContext.User,
            storeManager,
            tenantId,
            ResourceNodeTypes.LocalAccount,
            localAccountId,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        // Re-read to project the persisted values, including the row's assigned concurrency token.
        var account = await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            // Unreachable in practice; the account was just committed in this unit of work.
            return TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

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

    /// <summary>
    /// Updates a local account's profile fields.
    /// </summary>
    /// <response code="204">The local account was updated.</response>
    [EndpointName("api/local-accounts/update")]
    internal virtual async ValueTask<IResult> UpdateLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] JsonPatchDocument<UpdateLocalAccountRequest> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is not { } provisioner)
        {
            return LocalAccountsNotConfigured();
        }

        var openIdContext = httpContext.GetOpenIdContext();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var account = await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var model = new UpdateLocalAccountRequest
        {
            UserName = account.UserName,
            Email = account.Email,
            EmailVerified = account.EmailVerified,
        };

        try
        {
            request.ApplyTo(model);
        }
        catch (JsonPatchException exception)
        {
            Logger.JsonPatchFailed(exception);
            return TypedResults.Problem(
                detail: "The JSON Patch document could not be applied.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var error = await LocalAccountValidator.ValidateUpdateAsync(
            httpContext.User,
            tenantId,
            localAccountId,
            model.UserName,
            account.ConcurrencyToken,
            ifMatch,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        await provisioner.UpdateAsync(
            openIdContext,
            storeManager,
            new LocalAccountUpdateRequest
            {
                LocalAccountId = localAccountId,
                UserName = model.UserName,
                Email = model.Email,
                EmailVerified = model.EmailVerified,
            },
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Updated,
            null,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a local account, together with its self-issued connection identity and (when it owns no other
    /// connections) its principal.
    /// </summary>
    /// <response code="204">The local account was deleted.</response>
    [EndpointName("api/local-accounts/delete")]
    internal virtual async ValueTask<IResult> DeleteLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is null)
        {
            return LocalAccountsNotConfigured();
        }

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

        var resourceValues = ToResourceValues(ToLocalAccountResource(account));

        // A local account and its one-to-one self-issued connection identity are removed together; the owning
        // principal is removed only when the account was its sole connection (ADR-0057).
        var identityStore = storeManager.GetStore<IFederatedIdentityStore>();
        var identity = await identityStore.GetByIssuerSubjectAsync(
            AccountConstants.SelfIssuer,
            localAccountId,
            cancellationToken
        );

        await accountStore.RemoveAsync(localAccountId, cancellationToken);

        if (identity is not null)
        {
            var principalIdentities = await identityStore.GetByPrincipalAsync(
                identity.PrincipalId,
                cancellationToken
            );
            await identityStore.RemoveAsync(identity.FederatedIdentityId, cancellationToken);

            if (principalIdentities.Count <= 1)
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
            resourceValues,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Disables a local account.
    /// </summary>
    /// <response code="204">The local account was disabled.</response>
    [EndpointName("api/local-accounts/disable")]
    internal virtual ValueTask<IResult> DisableLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    ) => SetEnabledAsync(httpContext, tenantId, localAccountId, false, ifMatch, cancellationToken);

    /// <summary>
    /// Enables a local account.
    /// </summary>
    /// <response code="204">The local account was enabled.</response>
    [EndpointName("api/local-accounts/enable")]
    internal virtual ValueTask<IResult> EnableLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    ) => SetEnabledAsync(httpContext, tenantId, localAccountId, true, ifMatch, cancellationToken);

    private async ValueTask<IResult> SetEnabledAsync(
        HttpContext httpContext,
        string tenantId,
        string localAccountId,
        bool isEnabled,
        string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is not { } provisioner)
        {
            return LocalAccountsNotConfigured();
        }

        var openIdContext = httpContext.GetOpenIdContext();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var account = await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationFailure = await AuthorizeOrFailAsync(
            httpContext,
            tenantId,
            localAccountId,
            account.ConcurrencyToken,
            ifMatch
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await provisioner.SetEnabledAsync(
            openIdContext,
            storeManager,
            localAccountId,
            isEnabled,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Updated,
            null,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Resets a local account's credential, rotating its security stamp so outstanding sessions and tokens are
    /// invalidated.
    /// </summary>
    /// <response code="204">The credential was reset.</response>
    [EndpointName("api/local-accounts/password/reset")]
    internal virtual async ValueTask<IResult> ResetPasswordAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] ResetLocalAccountPasswordRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is not { } provisioner)
        {
            return LocalAccountsNotConfigured();
        }

        var openIdContext = httpContext.GetOpenIdContext();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var account = await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationFailure = await AuthorizeOrFailAsync(
            httpContext,
            tenantId,
            localAccountId,
            account.ConcurrencyToken,
            ifMatch
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await WithEncodedPasswordAsync(
            request.Password,
            async password =>
            {
                await provisioner.ResetPasswordAsync(
                    openIdContext,
                    storeManager,
                    new LocalAccountPasswordResetRequest
                    {
                        LocalAccountId = localAccountId,
                        Password = password,
                    },
                    cancellationToken
                );
                return true;
            }
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Updated,
            null,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Gets a local account's server-owned metadata bags.
    /// </summary>
    /// <response code="200">The account's metadata.</response>
    [EndpointName("api/local-accounts/metadata/get")]
    internal virtual async ValueTask<IResult> GetMetadataAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is null)
        {
            return LocalAccountsNotConfigured();
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var account = await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var principal = await ResolvePrincipalOrDefaultAsync(
            storeManager,
            localAccountId,
            cancellationToken
        );

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.LocalAccount, localAccountId);
        return await ProcessGetAsync(
            httpContext,
            principal,
            node,
            Operations.Read,
            resolved => ToLocalAccountMetadataResource(tenantId, localAccountId, resolved)
        );
    }

    /// <summary>
    /// Sets a local account's server-owned metadata bags. Each bag is replaced only when present in the request.
    /// Because the management API is administrator-gated, this is the server/admin write path through which
    /// <c>SystemMetadata</c> may be set; it is never end-user writable (ADR-0054, ADR-0055).
    /// </summary>
    /// <response code="204">The metadata was updated.</response>
    [EndpointName("api/local-accounts/metadata/set")]
    internal virtual async ValueTask<IResult> SetMetadataAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] SetLocalAccountMetadataRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is null)
        {
            return LocalAccountsNotConfigured();
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var account = await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.LocalAccount, localAccountId);
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            node,
            Operations.Update
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var principal = await ResolvePrincipalOrDefaultAsync(
            storeManager,
            localAccountId,
            cancellationToken
        );
        if (principal is null)
        {
            return TypedResults.NotFound();
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, principal.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

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
            .UpdateAsync(principal, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        // The metadata values may carry authorization-relevant data, so the audit snapshot records the change without
        // the values themselves.
        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Updated,
            null,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Lists a local account's owners.
    /// </summary>
    /// <response code="200">A page of the account's owners.</response>
    [EndpointName("api/local-accounts/owners/list")]
    internal virtual async ValueTask<IResult> ListOwnersAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is null)
        {
            return LocalAccountsNotConfigured();
        }

        return await ProcessListOwnersAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.LocalAccount,
            localAccountId,
            (storeManager, token) => LocalAccountExistsAsync(storeManager, localAccountId, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Adds an owner to a local account.
    /// </summary>
    /// <response code="201">The owner was added.</response>
    [EndpointName("api/local-accounts/owners/add")]
    internal virtual async ValueTask<IResult> AddOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] AddOwnerRequest request,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is null)
        {
            return LocalAccountsNotConfigured();
        }

        return await ProcessAddOwnerAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.LocalAccount,
            localAccountId,
            request,
            $"/tenants/{tenantId}/local-accounts/{localAccountId}/owners",
            (storeManager, token) => LocalAccountExistsAsync(storeManager, localAccountId, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Removes an owner from a local account.
    /// </summary>
    /// <response code="204">The owner was removed.</response>
    [EndpointName("api/local-accounts/owners/remove")]
    internal virtual async ValueTask<IResult> RemoveOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    )
    {
        if (LocalAccountProvisioner is null)
        {
            return LocalAccountsNotConfigured();
        }

        return await ProcessRemoveOwnerAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.LocalAccount,
            localAccountId,
            principalId,
            cancellationToken
        );
    }

    // Authorizes the caller for an Update on the account node and enforces the optional If-Match precondition. Returns
    // the failure result, or null when the caller may proceed.
    private async ValueTask<IResult?> AuthorizeOrFailAsync(
        HttpContext httpContext,
        string tenantId,
        string localAccountId,
        string concurrencyToken,
        string? ifMatch
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.LocalAccount, localAccountId);
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            node,
            Operations.Update
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, concurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        return null;
    }

    private static async ValueTask<PersistedFederatedPrincipal?> ResolvePrincipalOrDefaultAsync(
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

    private static async ValueTask<bool> LocalAccountExistsAsync(
        IStoreManager storeManager,
        string localAccountId,
        CancellationToken cancellationToken
    ) =>
        await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByIdOrDefaultAsync(localAccountId, cancellationToken)
            is not null;

    private static ProblemHttpResult LocalAccountsNotConfigured() =>
        TypedResults.Problem(
            detail: "Local accounts are not configured on this server.",
            statusCode: StatusCodes.Status501NotImplemented
        );

    // Encodes a plaintext password into a pooled, zeroed-on-return buffer so the credential never lingers on the heap,
    // then runs the supplied operation with the caller-owned bytes. The operation must consume the bytes before it
    // returns (the provisioner hashes synchronously).
    private static async ValueTask<T> WithEncodedPasswordAsync<T>(
        string password,
        Func<ReadOnlyMemory<byte>, ValueTask<T>> useAsync
    )
    {
        var byteCount = Encoding.UTF8.GetByteCount(password);
        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var written = Encoding.UTF8.GetBytes(password, buffer);
            return await useAsync(buffer.AsMemory(0, written));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer.AsSpan(0, byteCount));
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
