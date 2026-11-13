// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Management.Contracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Endpoints;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;

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
/// Identity adapter) that these endpoints operate over. Deleting or disabling an account, or resetting its password,
/// ends every session of its principal. Every password it sets meets the tenant's password policy; a refused password
/// is answered 400 with the requirement it fails. The security effects that follow a commit (ending the sessions of a
/// disabled account or of a reset password, and clearing failed password attempts) run through
/// <see cref="IPostCommitSecurityEffects"/>.
/// </summary>
internal class LocalAccountApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ICryptoService cryptoService,
    ILocalAccountValidator localAccountValidator,
    IManagementAuditRecorder managementAuditRecorder,
    ISessionService sessionService,
    IOpenIdContextFactory openIdContextFactory,
    LocalAccountPasswords localAccountPasswords,
    IPostCommitSecurityEffects postCommitSecurityEffects
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
    private LocalAccountPasswords LocalAccountPasswords { get; } = localAccountPasswords;
    private IPostCommitSecurityEffects PostCommitSecurityEffects { get; } =
        postCommitSecurityEffects;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var accounts = endpoints
            .MapGroup("/local-accounts")
            .WithTags(OpenIdConstants.EndpointTags.LocalAccounts);

        accounts
            .MapPost("", CreateLocalAccountAsync)
            .Produces<LocalAccountResource>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        accounts
            .MapGet("", ListLocalAccountsAsync)
            .Produces<CollectionResource<LocalAccountResource>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        accounts
            .MapGet("/{localAccountId}", GetLocalAccountAsync)
            .Produces<LocalAccountResource>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        accounts
            .MapPatch("/{localAccountId}", UpdateLocalAccountAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        accounts
            .MapDelete("/{localAccountId}", DeleteLocalAccountAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        accounts
            .MapPost("/{localAccountId}/enable", EnableLocalAccountAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        accounts
            .MapPost("/{localAccountId}/disable", DisableLocalAccountAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        accounts
            .MapPost("/{localAccountId}/password", ResetPasswordAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        accounts
            .MapGet("/{localAccountId}/claims", GetClaimsAsync)
            .Produces<LocalAccountClaimsResource>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        accounts
            .MapPut("/{localAccountId}/claims", SetClaimsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        accounts
            .MapGet("/{localAccountId}/metadata", GetMetadataAsync)
            .Produces<LocalAccountMetadataResource>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        accounts
            .MapPatch("/{localAccountId}/metadata", SetMetadataAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
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

    /// <summary>
    /// Creates a local account in a tenant, together with the principal and the self-issued identity that it resolves to.
    /// </summary>
    /// <response code="201">The created local account.</response>
    /// <response code="400">The password does not meet the tenant's password policy.</response>
    /// <response code="404">The tenant does not exist.</response>
    /// <response code="409">A local account with the username already exists in the tenant.</response>
    internal virtual async ValueTask<IResult> CreateLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] CreateLocalAccountRequest request,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await LocalAccountValidator.AuthorizeCreateAsync(
            httpContext.User,
            tenantId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var error = await LocalAccountValidator.ValidateCreateAsync(
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

        var passwordError = await LocalAccountPasswords.ValidateAsync(
            storeManager,
            tenantId,
            account,
            request.Password,
            cancellationToken
        );
        if (passwordError is not null)
        {
            return ToErrorResult(passwordError);
        }

        // Eagerly create the owning principal and the account's self-issued connection so the account resolves to a
        // principal uniformly with any federated identity (ADR-1051/ADR-1057).
        var principal = new PersistedFederatedPrincipal
        {
            TenantId = tenantId,
            PrincipalId = principalId,
            ConcurrencyToken = string.Empty,
        };

        // Set-when-present: a missing bag leaves the principal's default empty object (ADR-1055).
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

        await LocalAccountPasswords.WithPasswordBytesAsync(
            request.Password,
            (bytes, ct) => storeManager.GetStore<ILocalAccountStore>().AddAsync(account, bytes, ct),
            cancellationToken
        );

        await storeManager.SaveChangesAsync(cancellationToken);

        // The password an administrator sets replaces the one being guessed, so its failed attempts start over. The
        // change is committed, so clearing them runs on its own bounded token, a failure is logged, and the audit
        // record follows. See ADR-1237 and ADR-1351.
        await PostCommitSecurityEffects.ResetPasswordAttemptsAsync(tenantId, request.UserName);

        var resource = ToLocalAccountResource(account);

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Created,
            ToResourceValues(resource)
        );

        httpContext.Response.Headers.ETag = account.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/local-accounts/{localAccountId}",
            resource
        );
    }

    /// <summary>
    /// Lists the local accounts of a tenant, one page at a time.
    /// </summary>
    /// <response code="200">A page of local accounts.</response>
    internal virtual async ValueTask<IResult> ListLocalAccountsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            TenantScopeResource.For(tenantId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<ILocalAccountStore>();
                return await store.GetPageAsync(
                    new PageRequest { Cursor = cursor, Limit = effectiveLimit },
                    cancellationToken
                );
            },
            ToLocalAccountResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets a local account of a tenant.
    /// </summary>
    /// <response code="200">The local account.</response>
    /// <response code="404">The local account does not exist.</response>
    internal virtual async ValueTask<IResult> GetLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessGetAsync(
            httpContext,
            TenantScopeResource.For(tenantId),
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                return await storeManager
                    .GetStore<ILocalAccountStore>()
                    .GetByIdOrDefaultAsync(localAccountId, token);
            },
            ToLocalAccountResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Updates the username and the email of a local account.
    /// </summary>
    /// <response code="204">The local account was updated.</response>
    /// <response code="404">The local account does not exist.</response>
    /// <response code="409">Another local account in the tenant already has the username.</response>
    internal virtual async ValueTask<IResult> UpdateLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] UpdateLocalAccountRequest request,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await LocalAccountValidator.AuthorizeUpdateAsync(
            httpContext.User,
            tenantId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        var account = await store.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return ManagementProblems.ResourceNotFound();
        }

        var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
        var error = await LocalAccountValidator.ValidateUpdateAsync(
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
            ToResourceValues(ToLocalAccountResource(account))
        );

        httpContext.Response.Headers.ETag = account.ConcurrencyToken;
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a local account and ends the sessions of its principal.
    /// </summary>
    /// <response code="204">The local account was deleted.</response>
    /// <response code="404">The local account does not exist.</response>
    internal virtual async ValueTask<IResult> DeleteLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await LocalAccountValidator.AuthorizeDeleteAsync(
            httpContext.User,
            tenantId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var accountStore = storeManager.GetStore<ILocalAccountStore>();
        var account = await accountStore.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return ManagementProblems.ResourceNotFound();
        }

        var error = await LocalAccountValidator.ValidateDeleteAsync(
            localAccountId,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        // The tenant's context is created before anything is written, whether or not the account has a
        // self-issued identity: a tenant that is not served refuses the request here, so the answer depends on the
        // tenant, not on the account's shape.
        await using var tenantContext = await CreateTenantContextAsync(
            httpContext,
            tenantId,
            cancellationToken
        );

        // Cascade the account's self-issued connection, and the owning principal when this was its only identity.
        var identityStore = storeManager.GetStore<IFederatedIdentityStore>();
        var identity = await identityStore.GetByIssuerSubjectAsync(
            AccountConstants.SelfIssuer,
            localAccountId,
            cancellationToken
        );
        if (identity is not null)
        {
            // The principal's sessions end before the deletion commits, while they can still be listed to notify their relying parties. See ADR-1229.
            await TerminateSessionsAsync(
                tenantContext,
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
            resourceValues: null
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Enables a local account.
    /// </summary>
    /// <response code="204">The local account was enabled.</response>
    /// <response code="404">The local account does not exist.</response>
    internal virtual ValueTask<IResult> EnableLocalAccountAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    ) => SetEnabledAsync(httpContext, tenantId, localAccountId, isEnabled: true, cancellationToken);

    /// <summary>
    /// Disables a local account and ends the sessions of its principal.
    /// </summary>
    /// <response code="204">The local account was disabled.</response>
    /// <response code="404">The local account does not exist.</response>
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
        var authorizationError = await LocalAccountValidator.AuthorizeUpdateAsync(
            httpContext.User,
            tenantId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        var account = await store.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return ManagementProblems.ResourceNotFound();
        }

        // Disabling an account with a self-issued identity ends its principal's sessions, so the tenant's context is
        // created before the account is written: a tenant that is not served refuses the request here, before anything
        // is committed or audited. Enabling an account, or disabling one without a self-issued identity, ends no
        // session and needs no context.
        var principalId = isEnabled
            ? null
            : await FindSelfIssuedPrincipalIdAsync(storeManager, localAccountId, cancellationToken);

        await using var tenantContext = principalId is null
            ? null
            : await CreateTenantContextAsync(httpContext, tenantId, cancellationToken);

        account.IsEnabled = isEnabled;
        await store.UpdateAsync(account, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        if (tenantContext is not null && principalId is not null)
        {
            // A disabled account's sessions end, which revokes every grant of its principal and notifies its relying
            // parties. The change is committed, so the termination runs on its own bounded token, a failure is
            // logged, and the audit record follows. See ADR-1229 and ADR-1351.
            await PostCommitSecurityEffects.TerminateSessionsAsync(
                tenantContext,
                principalId,
                SessionTerminationReasons.AccountDisabled
            );
        }

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToLocalAccountResource(account))
        );

        httpContext.Response.Headers.ETag = account.ConcurrencyToken;
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Sets the password of a local account and ends the sessions of its principal.
    /// </summary>
    /// <response code="204">The password was set.</response>
    /// <response code="400">The password does not meet the tenant's password policy.</response>
    /// <response code="404">The local account or the tenant does not exist.</response>
    internal virtual async ValueTask<IResult> ResetPasswordAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] ResetLocalAccountPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await LocalAccountValidator.AuthorizeUpdateAsync(
            httpContext.User,
            tenantId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        var account = await store.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return ManagementProblems.ResourceNotFound();
        }

        var passwordError = await LocalAccountPasswords.ValidateAsync(
            storeManager,
            tenantId,
            account,
            request.Password,
            cancellationToken
        );
        if (passwordError is not null)
        {
            return ToErrorResult(passwordError);
        }

        // An account with a self-issued identity has its principal's sessions ended, so the tenant's context is created
        // before the password is written: a tenant that is not served refuses the request here, before anything is
        // committed or audited. An account without one ends no session and needs no context.
        var principalId = await FindSelfIssuedPrincipalIdAsync(
            storeManager,
            localAccountId,
            cancellationToken
        );

        await using var tenantContext = principalId is null
            ? null
            : await CreateTenantContextAsync(httpContext, tenantId, cancellationToken);

        await LocalAccountPasswords.WithPasswordBytesAsync(
            request.Password,
            (bytes, ct) => store.SetPasswordAsync(localAccountId, bytes, ct),
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        if (tenantContext is not null && principalId is not null)
        {
            // A reset password ends the sessions of the account's principal and revokes its grants, so the tokens of a
            // compromised account stop at once. The change is committed, so the termination runs on its own bounded
            // token, a failure is logged, and the audit record follows. See ADR-1229 and ADR-1351.
            await PostCommitSecurityEffects.TerminateSessionsAsync(
                tenantContext,
                principalId,
                SessionTerminationReasons.PasswordReset
            );
        }

        // The password an administrator sets replaces the one being guessed, so its failed attempts start over. The
        // change is committed, so clearing them runs on its own bounded token, a failure is logged, and the audit
        // record follows. See ADR-1237 and ADR-1351.
        await PostCommitSecurityEffects.ResetPasswordAttemptsAsync(tenantId, account.UserName);

        await ManagementAuditRecorder.RecordLocalAccountChangedAsync(
            httpContext,
            tenantId,
            localAccountId,
            ResourceChangeTypes.Updated,
            resourceValues: null
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Gets the claims of a local account.
    /// </summary>
    /// <response code="200">The claims of the local account.</response>
    /// <response code="404">The local account does not exist.</response>
    internal virtual async ValueTask<IResult> GetClaimsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessGetAsync(
            httpContext,
            TenantScopeResource.For(tenantId),
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                return await storeManager
                    .GetStore<ILocalAccountStore>()
                    .GetByIdOrDefaultAsync(localAccountId, token);
            },
            value => ToClaimsResource(tenantId, value),
            cancellationToken
        );
    }

    /// <summary>
    /// Replaces the claims of a local account.
    /// </summary>
    /// <response code="204">The claims were replaced.</response>
    /// <response code="404">The local account does not exist.</response>
    internal virtual async ValueTask<IResult> SetClaimsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] SetLocalAccountClaimsRequest request,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await LocalAccountValidator.AuthorizeUpdateAsync(
            httpContext.User,
            tenantId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        var account = await store.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        if (account is null)
        {
            return ManagementProblems.ResourceNotFound();
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
            resourceValues: null
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Gets the profile and system metadata of the principal of a local account.
    /// </summary>
    /// <response code="200">The metadata of the principal.</response>
    /// <response code="404">The local account does not exist, or it has no principal.</response>
    internal virtual async ValueTask<IResult> GetMetadataAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessGetAsync(
            httpContext,
            TenantScopeResource.For(tenantId),
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                return await ResolvePrincipalAsync(storeManager, localAccountId, token);
            },
            value => ToMetadataResource(tenantId, localAccountId, value),
            cancellationToken
        );
    }

    /// <summary>
    /// Sets the profile and system metadata that the request supplies on the principal of a local account.
    /// </summary>
    /// <response code="204">The metadata was set.</response>
    /// <response code="404">The local account does not exist, or it has no principal.</response>
    internal virtual async ValueTask<IResult> SetMetadataAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string localAccountId,
        [FromBody] SetLocalAccountMetadataRequest request,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await LocalAccountValidator.AuthorizeUpdateAsync(
            httpContext.User,
            tenantId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
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
            return ManagementProblems.ResourceNotFound();
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
            resourceValues: null
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
    // back-channel logout, auditing) in that tenant's security domain rather than the request's resolved tenant. A
    // tenant that is not served refuses here, so a route creates the context before its first write.
    private ValueTask<OpenIdContext> CreateTenantContextAsync(
        HttpContext httpContext,
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        return OpenIdContextFactory.CreateForTenantAsync(
            httpContext,
            tenantId,
            mediator,
            cancellationToken
        );
    }

    private async ValueTask TerminateSessionsAsync(
        OpenIdContext tenantContext,
        string principalId,
        string reason,
        CancellationToken cancellationToken
    ) => await SessionService.RevokeAllAsync(tenantContext, principalId, reason, cancellationToken);

    // Resolves the principal behind the account's self-issued identity, if the account has one. A route resolves it
    // before its write, so the effect that follows the commit is one call.
    private static async ValueTask<string?> FindSelfIssuedPrincipalIdAsync(
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
        return identity?.PrincipalId;
    }
}
