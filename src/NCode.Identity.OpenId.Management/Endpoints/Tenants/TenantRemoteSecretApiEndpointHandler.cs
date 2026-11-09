// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/*

POST api/tenants/{tenantId}/secrets/remote
POST api/tenants/{tenantId}/secrets/remote/import

*/

/// <summary>
/// Provides the API endpoints that manage the signing and encryption keys a tenant holds in the key management
/// service of a registered remote key provider. The private half of such a key never leaves the service.
/// </summary>
// See ADR-1475.
// See ADR-1477.
internal class TenantRemoteSecretApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ICryptoService cryptoService,
    ISecretValidator secretValidator,
    ISecretGenerator secretGenerator,
    IRemoteSecretImporter remoteSecretImporter,
    IRemoteSecretDestroyer remoteSecretDestroyer,
    IManagementAuditRecorder managementAuditRecorder,
    TimeProvider timeProvider,
    ILogger<TenantRemoteSecretApiEndpointHandler> logger
) : BaseApiEndpointHandler, IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ISecretValidator SecretValidator { get; } = secretValidator;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private IRemoteSecretImporter RemoteSecretImporter { get; } = remoteSecretImporter;
    private IRemoteSecretDestroyer RemoteSecretDestroyer { get; } = remoteSecretDestroyer;
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<TenantRemoteSecretApiEndpointHandler> Logger { get; } = logger;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var tenants = endpoints.MapGroup("/tenants").WithTags(OpenIdConstants.EndpointTags.Tenants);

        tenants
            .MapPost("/{tenantId}/secrets/remote", CreateRemoteSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        tenants
            .MapPost("/{tenantId}/secrets/remote/import", ImportRemoteSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    /// <summary>
    /// Creates a tenant's signing or encryption key in the key management service of a registered remote key provider.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The <see cref="CreateRemoteSecretRequest"/> describing the key to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created secret.</response>
    /// <response code="400">The request is not coherent or the key could not be created from its parameters.</response>
    /// <response code="401">The caller is anonymous.</response>
    /// <response code="403">The caller lacks the authority the request needs.</response>
    /// <response code="404">The tenant does not exist.</response>
    /// <response code="409">The tenant already holds a secret with the generated identifier.</response>
    /// <response code="412">Another request changed the tenant's secrets after this request read them; the created key is destroyed.</response>
    /// <response code="502">The key management service did not create the key.</response>
    // See ADR-1475.
    [EndpointName("api/tenants/secrets/create-remote")]
    internal virtual async ValueTask<IResult> CreateRemoteSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] CreateRemoteSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            GetTenantNode(tenantId),
            Operations.Create,
            cancellationToken
        );

        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var createdWhen = TimeProvider.GetUtcNow();
        var validation = SecretValidator.ValidateCreateRemote(request, createdWhen);
        if (!validation.IsValid)
        {
            return ToErrorResult(validation.Error);
        }

        var parameters =
            validation.GenerationParameters
            ?? throw new InvalidOperationException("A valid request has generation parameters.");

        var secretId = CryptoService.GenerateResourceId();

        var secretIdConflict = SecretIdConflictOrDefault(tenant.Secrets.Value, secretId);
        if (secretIdConflict is not null)
        {
            return secretIdConflict;
        }

        var generateRequest = new GenerateRemoteSecretRequest
        {
            SecretId = secretId,
            Owner = SecretOwner.ForTenant(tenantId),
            Provider = request.RemoteKeyProvider,
            SecretType = request.SecretType,
            KeySizeBits = parameters.KeySizeBits,
            Curve = parameters.Curve,
            Algorithm = request.Algorithm,
            CreatedWhen = createdWhen,
            ActivatesWhen = request.ActivatesWhen,
            RetiresWhen = request.RetiresWhen,
            ExpiresWhen = request.ExpiresWhen,
        };

        PersistedSecret secret;
        try
        {
            secret = await SecretGenerator.GenerateRemoteSecretAsync(
                generateRequest,
                cancellationToken
            );
        }
        catch (RemoteSecretRefusedException exception)
        {
            Logger.SecretGenerationFailed(exception);
            return TypedResults.Problem(
                detail: "The key could not be created in the key management service from the supplied parameters.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.RemoteKeyProviderFailed(request.RemoteKeyProvider, secretId, exception);
            return TypedResults.Problem(
                detail: $"The key management service of the remote key provider '{request.RemoteKeyProvider}' did not create the key.",
                statusCode: StatusCodes.Status502BadGateway
            );
        }

        try
        {
            await store.AddSecretAsync(tenantId, secret, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await RemoteSecretDestroyer.DestroyAsync([secret]);
            throw;
        }

        return await AnswerCreatedAsync(httpContext, tenantId, secret, cancellationToken);
    }

    /// <summary>
    /// Binds a signing or encryption key that the key management service of a registered remote key provider already
    /// holds to a tenant. The server reads the key's public half from the service, checks it against the request's
    /// algorithm and proves the key holds its private half — it signs a challenge or decrypts one — before it stores
    /// the key, and never destroys it.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The <see cref="ImportRemoteSecretRequest"/> naming the key, its type, size, curve and algorithm.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The bound secret.</response>
    /// <response code="400">The request is not coherent, the provider refuses the key, its public half is not a key of the request's type, size and curve, or the key does not prove it holds the private half.</response>
    /// <response code="401">The caller is anonymous.</response>
    /// <response code="403">The caller lacks the authority the request needs.</response>
    /// <response code="404">The tenant does not exist.</response>
    /// <response code="409">The tenant already holds a secret with the generated identifier or the same <c>kid</c>.</response>
    /// <response code="412">Another request changed the tenant's secrets after this request read them.</response>
    /// <response code="502">The key management service failed or refused reading the key's public half or the proof of the binding.</response>
    // See ADR-1475.
    // See ADR-1476.
    [EndpointName("api/tenants/secrets/import-remote")]
    internal virtual async ValueTask<IResult> ImportRemoteSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] ImportRemoteSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            GetTenantNode(tenantId),
            Operations.Create,
            cancellationToken
        );

        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        var platformRefusal = await GetPlatformRefusalOrDefaultAsync(
            httpContext,
            cancellationToken
        );
        if (platformRefusal is not null)
        {
            return platformRefusal;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var createdWhen = TimeProvider.GetUtcNow();
        var validation = SecretValidator.ValidateImportRemote(request, createdWhen);
        if (!validation.IsValid)
        {
            return ToErrorResult(validation.Error);
        }

        var parameters = validation.GenerationParameters;

        var secretId = CryptoService.GenerateResourceId();

        var secretIdConflict = SecretIdConflictOrDefault(tenant.Secrets.Value, secretId);
        if (secretIdConflict is not null)
        {
            return secretIdConflict;
        }

        var bindRequest = new BindRemoteSecretRequest
        {
            SecretId = secretId,
            Provider = request.RemoteKeyProvider,
            KeyId = request.RemoteKeyId,
            SecretType = request.SecretType,
            KeySizeBits = parameters.KeySizeBits,
            Curve = parameters.Curve,
            Algorithm = request.Algorithm,
            CreatedWhen = createdWhen,
            ActivatesWhen = request.ActivatesWhen,
            RetiresWhen = request.RetiresWhen,
            ExpiresWhen = request.ExpiresWhen,
        };

        var import = await RemoteSecretImporter.ImportAsync(bindRequest, cancellationToken);
        if (!import.IsBound)
        {
            return ToErrorResult(import.Error);
        }

        var secret = import.Secret;

        var keyIdConflict = KeyIdConflictOrDefault(tenant.Secrets.Value, secret, "tenant");
        if (keyIdConflict is not null)
        {
            return keyIdConflict;
        }

        var bindingError = await RemoteSecretImporter.VerifyBindingAsync(secret, cancellationToken);
        if (bindingError is not null)
        {
            return ToErrorResult(bindingError);
        }

        await store.AddSecretAsync(tenantId, secret, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return await AnswerCreatedAsync(httpContext, tenantId, secret, cancellationToken);
    }

    /// <summary>
    /// Records and announces a created tenant secret and answers <c>201 Created</c> with it.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secret">The created <see cref="PersistedSecret"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <c>201 Created</c> result carrying the secret.</returns>
    private async ValueTask<IResult> AnswerCreatedAsync(
        HttpContext httpContext,
        string tenantId,
        PersistedSecret secret,
        CancellationToken cancellationToken
    )
    {
        var secretResource = ToSecretResource(secret);

        await ManagementAuditRecorder.RecordTenantSecretChangedAsync(
            httpContext,
            tenantId,
            secret.SecretId,
            ResourceChangeTypes.Created,
            ToResourceValues(secretResource)
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenantId,
            settingsChanged: false,
            cancellationToken
        );

        // The store assigns the secret's token on insert (ADR-1012), so no re-read is needed.
        httpContext.Response.Headers.ETag = secret.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/secrets/{secret.SecretId}",
            secretResource
        );
    }

    /// <summary>
    /// Gets the refusal of a binding by a caller without <see cref="Operations.Update"/> authority at the platform
    /// node, or <c>null</c> when the caller may bind the key.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <c>403 Forbidden</c> result, or <c>null</c> when the caller may update the platform.</returns>
    // See ADR-1475.
    internal virtual async ValueTask<IResult?> GetPlatformRefusalOrDefaultAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            GetPlatformNode(httpContext),
            Operations.Update
        );
        if (authorizationResult.Succeeded)
        {
            return null;
        }

        return TypedResults.Problem(
            detail: "Binding a key that a key management service holds needs update authority on the platform: the deployment's identity reaches every key the service lets it use.",
            statusCode: StatusCodes.Status403Forbidden
        );
    }
}
